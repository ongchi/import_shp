using System.Collections.Generic;
using System.Linq;
using Eto.Drawing;
using Eto.Forms;
using Import_SHP.Import;
using Rhino;
using Rhino.Geometry;
using Rhino.UI;

namespace Import_SHP.UI
{
    /// <summary>The dialog that collects the import options in interactive mode.</summary>
    public sealed class ImportOptionsDialog : Dialog<bool>
    {
        private const string NoFieldItem = "(none)";

        private readonly RhinoDoc _doc;
        private readonly ShapefileSummary _summary;
        private readonly ImportOptions _options;

        /// <summary>True when an earlier import fixed the offset. The unit choice must not move it.</summary>
        private readonly bool _offsetComesFromDocument;

        private readonly TextBox _layerName = new();
        private readonly DropDown _modelUnits = new();
        private readonly DropDown _layoutUnits = new();
        private readonly DropDown _zSource = new();
        private readonly DropDown _zField = new();
        private readonly NumericStepper _constantZ = new() { DecimalPlaces = 3, MaximumDecimalPlaces = 6 };
        private readonly DropDown _nameField = new();
        private readonly CheckBox _applyOffset = new();
        private readonly NumericStepper _offsetX = new() { DecimalPlaces = 3, MaximumDecimalPlaces = 6, MinValue = double.MinValue, MaxValue = double.MaxValue };
        private readonly NumericStepper _offsetY = new() { DecimalPlaces = 3, MaximumDecimalPlaces = 6, MinValue = double.MinValue, MaxValue = double.MaxValue };
        private readonly CheckBox _groupParts = new();

        private ImportOptionsDialog(RhinoDoc doc, ShapefileSummary summary, ImportOptions options)
        {
            _doc = doc;
            _summary = summary;
            _options = options;
            _offsetComesFromDocument = OriginOffset.TryReadFromDocument(doc, out _);

            Title = "Import Shapefile";
            Padding = new Padding(10);
            Resizable = false;
            Result = false;

            BuildControls();
            Content = BuildLayout();
            UpdateEnabledState();
        }

        /// <summary>Shows the dialog and writes the user choices into <paramref name="options"/>.</summary>
        public static bool Show(RhinoDoc doc, ShapefileSummary summary, ImportOptions options)
        {
            var dialog = new ImportOptionsDialog(doc, summary, options);
            return dialog.ShowModal(RhinoEtoApp.MainWindowForDocument(doc));
        }

        private void BuildControls()
        {
            _layerName.Text = _options.LayerName;

            _zSource.Items.Add(new ListItem { Text = ZSourceText(), Key = nameof(ZSource.ShapeZ) });
            _zSource.Items.Add(new ListItem { Text = "Attribute field", Key = nameof(ZSource.AttributeField) });
            _zSource.Items.Add(new ListItem { Text = "Constant elevation", Key = nameof(ZSource.Constant) });
            _zSource.SelectedIndex = (int)_options.ZSource;
            _zSource.SelectedIndexChanged += (_, _) => UpdateEnabledState();

            FillFieldNames(_zField, _summary.NumericFieldNames, includeNoField: false);
            FillFieldNames(_nameField, _summary.FieldNames, includeNoField: true);

            _constantZ.Value = _options.ConstantZ;

            FillUnits(_modelUnits, _options.ModelUnits);
            FillUnits(_layoutUnits, _options.LayoutUnits);
            _modelUnits.SelectedIndexChanged += (_, _) => OnModelUnitsChanged();

            _applyOffset.Text = "Move the data near the world origin";
            _applyOffset.Checked = _options.ApplyOffset;
            _applyOffset.CheckedChanged += (_, _) => UpdateEnabledState();

            var offset = _options.ApplyOffset ? _options.Offset : SuggestOffset();
            _offsetX.Value = offset.X;
            _offsetY.Value = offset.Y;

            _groupParts.Text = "Group the parts of one record";
            _groupParts.Checked = _options.GroupParts;
        }

        private string ZSourceText()
        {
            return _summary.HasShapeZ ? "Z values of the shapes" : "Z values of the shapes (this file has none)";
        }

        private static void FillUnits(DropDown dropDown, UnitSystem selected)
        {
            foreach (var item in UnitChoice.Items)
                dropDown.Items.Add(new ListItem { Text = item.Label, Key = item.Unit.ToString() });

            dropDown.SelectedIndex = UnitChoice.IndexOf(selected);
        }

        /// <summary>
        /// The proposed offset follows the model unit, because the offset is in document units.
        /// </summary>
        private Vector3d SuggestOffset()
        {
            var bounds = _summary.Header.Bounds;
            if (bounds.IsEmpty)
                return Vector3d.Zero;

            var scale = UnitChoice.ScaleTo(SelectedUnits(_modelUnits), _doc.ModelUnitSystem);
            return OriginOffset.Suggest(bounds.CenterX * scale, bounds.CenterY * scale);
        }

        private void OnModelUnitsChanged()
        {
            if (_offsetComesFromDocument)
                return;

            var offset = SuggestOffset();
            _offsetX.Value = offset.X;
            _offsetY.Value = offset.Y;
        }

        private static UnitSystem SelectedUnits(DropDown dropDown) => UnitChoice.At(dropDown.SelectedIndex);

        private static void FillFieldNames(DropDown dropDown, IReadOnlyList<string> names, bool includeNoField)
        {
            if (includeNoField)
                dropDown.Items.Add(new ListItem { Text = NoFieldItem, Key = string.Empty });

            foreach (var name in names)
                dropDown.Items.Add(new ListItem { Text = name, Key = name });

            dropDown.SelectedIndex = dropDown.Items.Count > 0 ? 0 : -1;
            dropDown.Enabled = names.Count > 0;
        }

        private Control BuildLayout()
        {
            var layout = new DynamicLayout { DefaultSpacing = new Size(6, 6) };

            layout.AddRow(new Label { Text = "Layer" }, _layerName);
            layout.AddRow(new Label { Text = "Elevation from" }, _zSource);
            layout.AddRow(new Label { Text = "Elevation field" }, _zField);
            layout.AddRow(new Label { Text = "Constant elevation" }, _constantZ);
            layout.AddRow(new Label { Text = "Object name from" }, _nameField);
            layout.AddRow(new Label { Text = "Model units" }, _modelUnits);
            layout.AddRow(new Label { Text = "Layout units" }, _layoutUnits);
            layout.AddRow(new Label(), _applyOffset);
            layout.AddRow(new Label { Text = "Offset X" }, _offsetX);
            layout.AddRow(new Label { Text = "Offset Y" }, _offsetY);
            layout.AddRow(new Label(), _groupParts);

            var importButton = new Button { Text = "Import" };
            importButton.Click += (_, _) => Accept();
            var cancelButton = new Button { Text = "Cancel" };
            cancelButton.Click += (_, _) => Close(false);

            DefaultButton = importButton;
            AbortButton = cancelButton;

            layout.AddSeparateRow(null, importButton, cancelButton);
            return layout;
        }

        private void UpdateEnabledState()
        {
            var zSource = (ZSource)(_zSource.SelectedIndex < 0 ? 0 : _zSource.SelectedIndex);

            _zField.Enabled = zSource == ZSource.AttributeField && _summary.NumericFieldNames.Count > 0;
            _constantZ.Enabled = zSource == ZSource.Constant;

            var offsetEnabled = _applyOffset.Checked == true;
            _offsetX.Enabled = offsetEnabled;
            _offsetY.Enabled = offsetEnabled;
        }

        private void Accept()
        {
            var zSource = (ZSource)(_zSource.SelectedIndex < 0 ? 0 : _zSource.SelectedIndex);
            if (zSource == ZSource.AttributeField && _summary.NumericFieldNames.Count == 0)
            {
                Dialogs.ShowMessage("The shapefile has no numeric field. Select another elevation source.", "Import Shapefile");
                return;
            }

            _options.LayerName = _layerName.Text;
            _options.ZSource = zSource;
            _options.ZFieldName = SelectedKey(_zField);
            _options.ConstantZ = _constantZ.Value;
            _options.NameFieldName = SelectedKey(_nameField);
            _options.ModelUnits = SelectedUnits(_modelUnits);
            _options.LayoutUnits = SelectedUnits(_layoutUnits);
            _options.GroupParts = _groupParts.Checked == true;
            _options.ApplyOffset = _applyOffset.Checked == true;
            _options.Offset = _options.ApplyOffset ? new Vector3d(_offsetX.Value, _offsetY.Value, 0.0) : Vector3d.Zero;

            Close(true);
        }

        private static string SelectedKey(DropDown dropDown)
        {
            var item = dropDown.SelectedIndex >= 0 ? dropDown.Items.ElementAtOrDefault(dropDown.SelectedIndex) : null;
            return item?.Key ?? string.Empty;
        }
    }
}
