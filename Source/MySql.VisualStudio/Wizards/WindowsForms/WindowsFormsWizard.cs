﻿// Copyright © 2008, 2016, Oracle and/or its affiliates. All rights reserved.
//
// MySQL for Visual Studio is licensed under the terms of the GPLv2
// <http://www.gnu.org/licenses/old-licenses/gpl-2.0.html>, like most
// MySQL Connectors. There are special exceptions to the terms and
// conditions of the GPLv2 as it is applied to this software, see the
// FLOSS License Exception
// <http://www.mysql.com/about/legal/licensing/foss-exception.html>.
//
// This program is free software; you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published
// by the Free Software Foundation; version 2 of the License.
//
// This program is distributed in the hope that it will be useful, but
// WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
// or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License
// for more details.
//
// You should have received a copy of the GNU General Public License along
// with this program; if not, write to the Free Software Foundation, Inc.,
// 51 Franklin St, Fifth Floor, Boston, MA 02110-1301  USA

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using EnvDTE;
using Microsoft.VisualStudio.TemplateWizard;
using MySql.Data.MySqlClient;
using MySql.Data.VisualStudio.Properties;
using MySql.Data.VisualStudio.SchemaComparer;
using MySql.Data.VisualStudio.ServerInstances;
using MySql.Utility.Classes;
using MySql.Utility.Forms;
using VSLangProj;
using Process = System.Diagnostics.Process;

namespace MySql.Data.VisualStudio.Wizards.WindowsForms
{
  /// <summary>
  ///  Wizard for generation of a Windows Forms based project.
  /// </summary>
  public class WindowsFormsWizard : BaseWizard<WindowsFormsWizardForm, WindowsFormsCodeGeneratorStrategy>
  {
    internal MySqlConnection Connection { get; set; }

    private bool _hasDataGridDateColumn;

    private IdentedStreamWriter _sw;

    public WindowsFormsWizard(LanguageGenerator language)
      : base(language)
    {
      WizardForm = new WindowsFormsWizardForm(this);
      projectType = ProjectWizardType.WindowsForms;
    }

    /// <summary>
    /// If there is a DateTimePicker column and a grid layout, add the support code for custom DateTimePicker for Grids.
    /// </summary>
    /// <param name="vsProj"></param>
    /// <param name="columns"></param>
    /// <param name="detailColumns"></param>
    private void EnsureCodeForDateTimeGridColumn(VSProject vsProj, Dictionary<string, Column> columns, Dictionary<string, Column> detailColumns)
    {
      bool hasDateColumn = false;
      foreach (KeyValuePair<string, Column> kvp in columns)
      {
        if (!kvp.Value.IsDateType())
        {
          continue;
        }

        hasDateColumn = true;
        break;
      }

      if (!hasDateColumn && detailColumns != null)
      {
        if (detailColumns.Any(kvp => kvp.Value.IsDateType()))
        {
          hasDateColumn = true;
        }
      }

      // If is the case, then add support code.
      if (hasDateColumn)
      {
        string outFilePath = "";
        Stream stream = null;
        switch (Language)
        {
          case LanguageGenerator.CSharp:
            stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("MySql.Data.VisualStudio.Wizards.WindowsForms.Templates.CS.MyDateTimePickerColumn.cs");
            outFilePath = Path.Combine(ProjectPath, "MyDateTimePickerColumn.cs");
            break;

          case LanguageGenerator.VBNET:
            stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("MySql.Data.VisualStudio.Wizards.WindowsForms.Templates.VB.MyDateTimePickerColumn.vb");
            outFilePath = Path.Combine(ProjectPath, "MyDateTimePickerColumn.vb");
            break;
        }

        if (stream == null)
        {
          return;
        }

        using (var sr = new StreamReader(stream))
        {
          string contents = sr.ReadToEnd();
          File.WriteAllText(outFilePath, contents.Replace("$ProjectNamespace$", ProjectNamespace));
        }

        vsProj.Project.ProjectItems.AddFromFile(outFilePath);
      }

      _hasDataGridDateColumn = hasDateColumn;
    }

    public override void ProjectFinishedGenerating(Project project)
    {
#if NET_40_OR_GREATER
      var vsProj = project.Object as VSProject;
      var tables = new SortedSet<string>();
      var strategies = new Dictionary<string, WindowsFormsCodeGeneratorStrategy>();
      if (vsProj == null)
      {
        return;
      }

      vsProj.References.Add("MySql.Data");
      project.DTE.SuppressUI = true;
      vsProj.Project.Save();

      bool found = false;
      foreach (Reference reference in vsProj.References)
      {
        if (reference.Name.IndexOf("MySql.Data", StringComparison.CurrentCultureIgnoreCase) >= 0 && !String.IsNullOrEmpty(reference.Path))
        {
          found = true;
          break;
        }
      }

      if (!found)
      {
        var infoResult = InfoDialog.ShowDialog(
          InfoDialogProperties.GetOkCancelDialogProperties(
            InfoDialog.InfoType.Warning,
            Resources.MySqlDataProviderPackage_ConnectorNetNotFoundError,
            @"To use it you must download and install the MySQL Connector/Net package from http://dev.mysql.com/downloads/connector/net/",
            Resources.MySqlDataProviderPackage_ClickOkOrCancel));
        if (infoResult.DialogResult == DialogResult.OK)
        {
          ProcessStartInfo browserInfo = new ProcessStartInfo("http://dev.mysql.com/downloads/connector/net/");
          Process.Start(browserInfo);
        }
      }

      try
      {
        foreach (DbTables t in WizardForm.SelectedTables)
        {
          AdvancedWizardForm crud = WizardForm.CrudConfiguration[t.Name];
          // Ensure all model exists, even if user didn't went through validation pages.
          // So metadata for table used in FKs is already loaded.
          crud.GenerateModels();
          string detailTableName = crud.DetailTableName;
          string canonicalTableName = GetCanonicalIdentifier(crud.TableName);
          string canonicalDetailTableName = GetCanonicalIdentifier(detailTableName);
          // Gather all the tables
          tables.Add(crud.TableName);
          if (!string.IsNullOrEmpty(detailTableName))
            tables.Add(detailTableName);
          foreach (KeyValuePair<string, ForeignKeyColumnInfo> kvp2 in crud.ForeignKeys)
          {
            tables.Add(kvp2.Value.ReferencedTableName);
          }
          foreach (KeyValuePair<string, ForeignKeyColumnInfo> kvp2 in crud.DetailForeignKeys)
          {
            tables.Add(kvp2.Value.ReferencedTableName);
          }

          AddColumnMappings(canonicalTableName, crud.ValidationColumns);
          if (!string.IsNullOrEmpty(detailTableName))
          {
            AddColumnMappings(canonicalDetailTableName, crud.ValidationColumnsDetail);
          }

          InitializeColumnMappings(crud.ForeignKeys);
          InitializeColumnMappings(crud.DetailForeignKeys);
        }

        // Generate the model using the proper technology
        if (WizardForm.DataAccessTechnology == DataAccessTechnology.EntityFramework5 ||
            WizardForm.DataAccessTechnology == DataAccessTechnology.EntityFramework6)
        {
          if (WizardForm.DataAccessTechnology == DataAccessTechnology.EntityFramework5)
            CurrentEntityFrameworkVersion = ENTITY_FRAMEWORK_VERSION_5;
          else
            CurrentEntityFrameworkVersion = ENTITY_FRAMEWORK_VERSION_6;

          AddNugetPackage(vsProj, ENTITY_FRAMEWORK_PCK_NAME, CurrentEntityFrameworkVersion, true);
          GenerateEntityFrameworkModel(project, vsProj, WizardForm.Connection, "Model1", tables.ToList(), ProjectPath);
        }
        else if (WizardForm.DataAccessTechnology == DataAccessTechnology.TypedDataSet)
        {
          PopulateColumnMappingsForTypedDataSet();
          GenerateTypedDataSetModel(vsProj, WizardForm.Connection, tables.ToList());
        }

        try
        {
          _hasDataGridDateColumn = false;
          // Start a loop here, to generate screens for all the selected tables.
          for (int i = 0; i < WizardForm.SelectedTables.Count; i++)
          {
            AdvancedWizardForm crud = WizardForm.CrudConfiguration[WizardForm.SelectedTables[i].Name];
            Dictionary<string, Column> columns = crud.Columns;
            Dictionary<string, Column> detailColumns = crud.DetailColumns;
            string canonicalTableName = GetCanonicalIdentifier(crud.TableName);
            string detailTableName = crud.DetailTableName;
            //string canonicalDetailTableName = GetCanonicalIdentifier(detailTableName);

            if (!TablesIncludedInModel.ContainsKey(crud.TableName))
            {
              SendToGeneralOutputWindow(string.Format("Skipping generation of screen for table '{0}' because it does not have primary key.", crud.TableName));
              continue;
            }

            if ((crud.GuiType == GuiType.MasterDetail) && !TablesIncludedInModel.ContainsKey(crud.DetailTableName))
            {
              // If Detail table does not have PK, then you cannot edit details, "degrade" layout from Master Detail to Individual Controls.
              crud.GuiType = GuiType.IndividualControls;
              SendToGeneralOutputWindow(string.Format(
                "Degrading layout for table '{0}' from master detail to single controls (because detail table '{1}' does not have primary key).",
                crud.TableName, crud.DetailTableName));
            }

            // Create the strategy
            StrategyConfig config = new StrategyConfig(_sw, canonicalTableName, columns, detailColumns,
              WizardForm.DataAccessTechnology, crud.GuiType, Language,
              crud.ValidationsEnabled, crud.ValidationColumns, crud.ValidationColumnsDetail,
              WizardForm.ConnectionStringWithIncludedPassword, WizardForm.ConnectionString, crud.TableName,
              detailTableName, crud.ConstraintName, crud.ForeignKeys, crud.DetailForeignKeys);
            WindowsFormsCodeGeneratorStrategy strategy = WindowsFormsCodeGeneratorStrategy.GetInstance(config);
            strategies.Add(WizardForm.SelectedTables[i].Name, strategy);

            if (!_hasDataGridDateColumn)
            {
              EnsureCodeForDateTimeGridColumn(vsProj, columns, detailColumns);
            }

            string frmName = string.Format("frm{0}", canonicalTableName);
            //string frmDesignerName = string.Format("frm{0}.designer", canonicalTableName);
            // Add new form to project.
            AddNewForm(project, frmName, strategy);
          }
        }
        catch (WizardException e)
        {
          SendToGeneralOutputWindow(string.Format("An error ocurred: {0}\n\n{1}", e.Message, e.StackTrace));
        }

        // Now generated the bindings & custom code
        List<string> formNames = new List<string>();
        List<string> tableNames = new List<string>();
        for (int i = 0; i < WizardForm.SelectedTables.Count; i++)
        {
          AdvancedWizardForm crud = WizardForm.CrudConfiguration[WizardForm.SelectedTables[i].Name];
          string canonicalTableName = GetCanonicalIdentifier(crud.TableName);
          if (!TablesIncludedInModel.ContainsKey(crud.TableName))
            continue;

          string frmName = string.Format("frm{0}", canonicalTableName);
          formNames.Add(frmName);
          tableNames.Add(crud.TableName);
          string frmDesignerName = string.Format("frm{0}.designer", canonicalTableName);
          WindowsFormsCodeGeneratorStrategy strategy = strategies[WizardForm.SelectedTables[i].Name];
          AddBindings(vsProj, strategy, frmName, frmDesignerName);
        }
        // Add menu entries for each form
        AddMenuEntries(vsProj, formNames, tableNames);

        if (WizardForm.DataAccessTechnology == DataAccessTechnology.EntityFramework6)
        {
          // Change target version to 4.5 (only version currently supported for EF6).
          project.Properties.Item("TargetFrameworkMoniker").Value = ".NETFramework,Version=v4.5";
          // This line is a hack to avoid "Project Unavailable" exceptions.
          project = (Project)((Array)(Dte.ActiveSolutionProjects)).GetValue(0);
          vsProj = project.Object as VSProject;
        }

        RemoveTemplateForm(vsProj);

        FixNamespaces();

        SendToGeneralOutputWindow("Building Solution...");
        project.DTE.Solution.SolutionBuild.Build(true);

        Settings.Default.WinFormsWizardConnection = WizardForm.ConnectionName;
        Settings.Default.Save();

        SendToGeneralOutputWindow("Finished project generation.");

        if (project.DTE.Solution.SolutionBuild.LastBuildInfo > 0)
        {
          InfoDialog.ShowDialog(InfoDialogProperties.GetErrorDialogProperties(Resources.ErrorTitle, Resources.WindowsFormsWizard_SolutionBuildFailed));
        }

        WizardForm.Dispose();
      }
      catch (WizardException e)
      {
        SendToGeneralOutputWindow(string.Format("An error ocurred: {0}\n\n{1}", e.Message, e.StackTrace));
      }
#else
      throw new NotImplementedException();
#endif
    }

    protected override string GetConnectionString()
    {
      return WizardForm.ConnectionStringWithIncludedPassword;
    }

    internal virtual void RemoveTemplateForm(VSProject proj)
    {
    }

    /// <summary>
    ///  Creates and adds a new Windows Forms to the project.
    /// </summary>
    /// <param name="project"></param>
    /// <param name="formName"></param>
    /// <param name="strategy"></param>
    private void AddNewForm(Project project, string formName, WindowsFormsCodeGeneratorStrategy strategy)
    {
      //project.ProjectItems.Item(1).Remove();
      string formFile = Path.Combine(ProjectPath, strategy.GetFormFileName().Replace("Form1", formName));
      string formFileDesigner = Path.Combine(ProjectPath, strategy.GetFormDesignerFileName().Replace("Form1", formName));
      string formFileResx = Path.Combine(ProjectPath, strategy.GetFormResxFileName().Replace("Form1", formName));

      var contents = File.ReadAllText(Path.Combine(ProjectPath, strategy.GetFormFileName()));
      contents = contents.Replace("Form1", formName);
      File.WriteAllText(formFile, contents);

      contents = File.ReadAllText(Path.Combine(ProjectPath, strategy.GetFormDesignerFileName()));
      contents = contents.Replace("Form1", formName);
      File.WriteAllText(formFileDesigner, contents);

      contents = File.ReadAllText(Path.Combine(ProjectPath, strategy.GetFormResxFileName()));
      contents = contents.Replace("Form1", formName);
      File.WriteAllText(formFileResx, contents);

      // Now add the form
      ProjectItem pi = project.ProjectItems.AddFromFile(formFile);
      //ProjectItem pi2 = pi.ProjectItems.AddFromFile(formFileDesigner);
      ProjectItem pi3 = pi.ProjectItems.AddFromFile(formFileResx);
      pi3.Properties.Item("ItemType").Value = "EmbeddedResource";
      //pi.Properties.Item("ItemType").Value = "Compile";
      pi.Properties.Item("SubType").Value = "Form";
    }

    internal void InitializeColumnMappings(Dictionary<string, ForeignKeyColumnInfo> fks)
    {
      foreach (KeyValuePair<string, ForeignKeyColumnInfo> kvp in fks)
      {
        string fkTableName = kvp.Value.ReferencedTableName;
        if (string.IsNullOrEmpty(fkTableName)) continue;
        if (ColumnMappings.ContainsKey(fkTableName)) continue;
        Dictionary<string, Column> dicCols = GetColumnsFromTable(fkTableName, WizardForm.Connection);
        List<ColumnValidation> myColValidations = ValidationsGrid.GetColumnValidationList(fkTableName, dicCols, null);
        ColumnMappings.Add(fkTableName, myColValidations.ToDictionary(p => { return p.Name; }));
      }
    }

    /// <summary>
    /// Fixes namespaces for issue in VB.NET with some VS versions like 2013.
    /// </summary>
    private void FixNamespaces()
    {
      if (Language != LanguageGenerator.VBNET) return;
      string outputPath = Path.Combine(Path.Combine(ProjectPath, "My Project"), "Application.Designer.vb");
      string contents = File.ReadAllText(outputPath);
      if (WizardForm.DataAccessTechnology == DataAccessTechnology.EntityFramework6)
      {
        contents = contents.Replace(string.Format("Me.MainForm = Global.{0}.frmMain", ProjectNamespace),
          string.Format("Me.MainForm = Global.{0}.{0}.frmMain", ProjectNamespace));
      }
      else
      {
        contents = contents.Replace(string.Format("Me.MainForm = Global.{0}.frmMain", ProjectNamespace),
          string.Format("Me.MainForm = {0}.frmMain", ProjectNamespace));
      }
      File.WriteAllText(outputPath, contents);
    }

    public override void RunStarted(object automationObject, Dictionary<string, string> replacementsDictionary, WizardRunKind runKind, object[] customParams)
    {
      Dte = automationObject as DTE;

      connections = MySqlServerExplorerConnections.LoadMySqlConnectionsFromServerExplorer(Dte);
      WizardForm.connections = connections;
      WizardForm.dte = Dte;
      base.RunStarted(automationObject, replacementsDictionary, runKind, customParams);
    }

    protected virtual void AddMenuEntries(VSProject vsProj, List<string> formNames, List<string> tableNames)
    {
    }

    protected virtual void WriteMenuHandler(StreamWriter sw, string formName)
    {
    }

    protected virtual void WriteMenuStripConstruction(StreamWriter sw, string formName)
    {
    }

    protected virtual void WriteMenuAddRange(StreamWriter sw, string formName)
    {
    }

    protected virtual void WriteMenuControlInit(StreamWriter sw, string formName, string tableName)
    {
    }

    protected virtual void WriteAddRangeBegin(StreamWriter sw)
    {
    }

    protected virtual void WriteAddRangeEnd(StreamWriter sw)
    {
    }

    protected virtual void WriteMenuDeclaration(StreamWriter sw, string formName)
    {
    }

    protected virtual string MenuEventHandlerMarker { get { return ""; } }

    protected virtual string MenuDesignerControlDeclMarker { get { return ""; } }

    protected virtual string MenuDesignerControlInitMarker { get { return ""; } }

    protected virtual string MenuDesignerBeforeSuspendLayout { get { return ""; } }

    protected void WriteMenuEntries(string path, List<string> formNames)
    {
      string originalContents = File.ReadAllText(path);
      FileStream fs = new FileStream(path, FileMode.Truncate, FileAccess.Write, FileShare.Read, 16284);
      using (StringReader sr = new StringReader(originalContents))
      {
        using (StreamWriter sw = new StreamWriter(fs))
        {
          string line;
          while ((line = sr.ReadLine()) != null)
          {
            if (line.Trim() == MenuEventHandlerMarker)
            {
              for (int i = 0; i < formNames.Count; i++)
              {
                string formName = formNames[i];
                WriteMenuHandler(sw, formName);
              }
            }
            else
            {
              sw.WriteLine(line);
            }
          }
        }
      }
    }

    protected void WriteMenuDesignerEntries(string path, List<string> formNames, List<string> tableNames)
    {
      string originalContents = File.ReadAllText(path);
      FileStream fs = new FileStream(path, FileMode.Truncate, FileAccess.Write, FileShare.Read, 16284);
      using (StringReader sr = new StringReader(originalContents))
      {
        using (StreamWriter sw = new StreamWriter(fs))
        {
          string line;
          while ((line = sr.ReadLine()) != null)
          {
            if (line.Trim() == MenuDesignerBeforeSuspendLayout)
            {
              for (int i = 0; i < formNames.Count; i++)
              {
                string formName = formNames[i];
                WriteMenuStripConstruction(sw, formName);
              }
            }
            else if (line.Trim() == MenuDesignerControlInitMarker)
            {
              WriteAddRangeBegin(sw);
              for (int i = 0; i < formNames.Count; i++)
              {
                string formName = formNames[i];
                WriteMenuAddRange(sw, formName);
                if (i < formNames.Count - 1)
                {
                  sw.Write(", ");
                }
              }
              WriteAddRangeEnd(sw);

              for (int i = 0; i < formNames.Count; i++)
              {
                string formName = formNames[i];
                string tableName = tableNames[i];
                WriteMenuControlInit(sw, formName, tableName);
              }
            }
            else if (line.Trim() == MenuDesignerControlDeclMarker)
            {
              for (int i = 0; i < formNames.Count; i++)
              {
                string formName = formNames[i];
                WriteMenuDeclaration(sw, formName);
              }
            }
            else
            {
              sw.WriteLine(line);
            }
          }
        }
      }
    }

    private void AddBindings(VSProject vsProj, WindowsFormsCodeGeneratorStrategy strategy, string frmName, string frmDesignerName)
    {
      string ext = strategy.GetExtension();
      SendToGeneralOutputWindow(string.Format("Customizing Form {0} Code...", frmName));
      // Get Form.cs
      ProjectItem item = FindProjectItem(vsProj.Project.ProjectItems, frmName + ext);
      // Get Form.Designer.cs
      ProjectItem itemDesigner = FindProjectItem(item.ProjectItems, frmDesignerName + ext);

      AddBindings((string)(item.Properties.Item("FullPath").Value), strategy);
      AddBindings((string)(itemDesigner.Properties.Item("FullPath").Value), strategy);
    }

    private void AddBindings(string formPath, WindowsFormsCodeGeneratorStrategy strategy)
    {
      string originalContents = File.ReadAllText(formPath);
      FileStream fs = new FileStream(formPath, FileMode.Truncate, FileAccess.Write, FileShare.Read, 16284);
      using (StringReader sr = new StringReader(originalContents))
      {
        using (_sw = new IdentedStreamWriter(fs))
        {
          strategy.Writer = _sw;
          string line;
          while ((line = sr.ReadLine()) != null)
          {
            strategy.Execute(line);
          }
        } // using StreamWriter
      } // using StreamReader
    }
  }
}