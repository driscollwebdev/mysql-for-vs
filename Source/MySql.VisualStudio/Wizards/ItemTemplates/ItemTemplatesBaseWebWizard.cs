﻿// Copyright © 2015, 2016 Oracle and/or its affiliates. All rights reserved.
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

using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TemplateWizard;
using Microsoft.VisualStudio.TextTemplating;
using Microsoft.VisualStudio.TextTemplating.VSHost;
using MySql.Data.MySqlClient;
using MySql.Data.VisualStudio.Properties;
using MySql.Data.VisualStudio.SchemaComparer;
using MySql.Data.VisualStudio.Wizards.Web;
using MySql.Data.VisualStudio.Wizards.WindowsForms;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using MySql.Utility.Classes;
using MySql.Utility.Forms;
using VSLangProj;

namespace MySql.Data.VisualStudio.Wizards.ItemTemplates
{
  public class ItemTemplatesBaseWebWizard : IWizard
  {
    #region [ Private and internal fields ]
    private DTE dte;
    private string _projectPath;
    private string _projectNamespace;
    private string _itemTemplateTempPath;
    private string _NetFxVersion;
    private IVsOutputWindowPane _generalPane;
    private Dictionary<string, ForeignKeyColumnInfo> ForeignKeys = new Dictionary<string, ForeignKeyColumnInfo>();
    internal Dictionary<string, ForeignKeyColumnInfo> DetailForeignKeys = new Dictionary<string, ForeignKeyColumnInfo>();
    private string _selectedModel;
    private string _selectedEntity;
    private string _connectionString;
    private string _connectionName;
    private MySqlConnection _connection;
    internal Dictionary<string, Dictionary<string, ColumnValidation>> ColumnMappings = new Dictionary<string, Dictionary<string, ColumnValidation>>();
    internal Dictionary<string, string> TablesIncludedInModel = new Dictionary<string, string>();
    private DataAccessTechnology _dataAccessTechnology;
    private LanguageGenerator _language;
    private ItemTemplateUtilities.ProjectWizardType _projectType;
    private List<DbTables> _selectedTables;
    private string _currentEntityFrameworkVersion;
    internal protected readonly static string ENTITY_FRAMEWORK_VERSION_5 = "5.0.0";
    internal protected readonly static string ENTITY_FRAMEWORK_VERSION_6 = "6.0.0";
    private string T4Templates_Path = @"..\IDE\Extensions\Oracle\MySQL for Visual Studio\";
    private string cSharpControllerClass_FileName = @"\T4Templates\CSharp\CSharpControllerClass.tt";
    private string cSharpIndexFile_FileName = @"\T4Templates\CSharp\CSharpIndexFile.tt";
    private string vbControllerClass_FileName = @"\T4Templates\CSharp\VisualBasicControllerClass.tt";
    private string vbIndexFile_FileName = @"\T4Templates\CSharp\VisualBasicIndexFile.tt";
    #endregion

    #region [ IWIzard implementaion ]
    /// <summary>
    /// Runs custom wizard logic before opening an item in the template.
    /// </summary>
    /// <param name="projectItem">The project item that will be opened.</param>
    public void BeforeOpeningFile(ProjectItem projectItem)
    {
      return;
    }

    /// <summary>
    /// Runs custom wizard logic when a project has finished generating.
    /// </summary>
    /// <param name="project">The project that finished generating.</param>
    public void ProjectFinishedGenerating(Project project)
    {
      return;
    }

    /// <summary>
    /// Runs custom wizard logic when a project item has finished generating.
    /// </summary>
    /// <param name="projectItem">The project item that finished generating.</param>
    public void ProjectItemFinishedGenerating(ProjectItem projectItem)
    {
      return;
    }

    /// <summary>
    /// Runs custom wizard logic at the beginning of a template wizard run.
    /// </summary>
    /// <param name="automationObject">The automation object being used by the template wizard.</param>
    /// <param name="replacementsDictionary">The list of standard parameters to be replaced.</param>
    /// <param name="runKind">A <see cref="T:Microsoft.VisualStudio.TemplateWizard.WizardRunKind" /> indicating the type of wizard run.</param>
    /// <param name="customParams">The custom parameters with which to perform parameter replacement in the project.</param>
    public void RunStarted(Object automationObject, Dictionary<string, string> replacementsDictionary, WizardRunKind runKind, Object[] customParams)
    {
      try
      {
        dte = automationObject as DTE;
        Array activeProjects = (Array)dte.ActiveSolutionProjects;
        Project activeProj = (Project)activeProjects.GetValue(0);
        _projectPath = System.IO.Path.GetDirectoryName(activeProj.FullName);
        _projectNamespace = activeProj.Properties.Item("DefaultNamespace").Value.ToString();
        _NetFxVersion = replacementsDictionary["$targetframeworkversion$"];
        _itemTemplateTempPath = customParams[0].ToString().Substring(0, customParams[0].ToString().LastIndexOf("\\"));
        ItemTemplatesWebWizard form = new ItemTemplatesWebWizard(_language, dte, _projectType);
        form.StartPosition = FormStartPosition.CenterScreen;
        DialogResult result = form.ShowDialog();
        if (result == DialogResult.OK)
        {
          GetFormValues(form);
          string providerConnectionString = ItemTemplateUtilities.GetProviderConnectionString(_selectedModel, dte, false);
          if (!string.IsNullOrEmpty(providerConnectionString))
          {
            _connectionString = providerConnectionString;
            _connectionName = ItemTemplateUtilities.GetConnectionStringName(_selectedModel, dte, false);
            _connection = new MySqlConnection(_connectionString);
          }
        }
      }
      catch (WizardCancelledException wce)
      {
        throw wce;
      }
      catch (Exception e)
      {
        SendToGeneralOutputWindow(string.Format("An error occurred: {0}\n\n {1}", e.Message, e.StackTrace));
      }
    }

    /// <summary>
    /// Runs custom wizard logic when the wizard has completed all tasks.
    /// </summary>
    public void RunFinished()
    {
      string frmName = string.Empty;
      Array activeProjects = (Array)dte.ActiveSolutionProjects;
      Project project = null;
      VSProject vsProj = null;
      project = (Project)activeProjects.GetValue(0);

      if (project == null)
      {
        return;
      }

      vsProj = project.Object as VSProject;
      var tables = new List<string>();
      Settings.Default.MVCWizardConnection = _connectionString;
      Settings.Default.Save();
      if (_generalPane != null)
      {
        _generalPane.Activate();
      }

      SendToGeneralOutputWindow("Starting project generation...");
      if (_selectedTables != null && _dataAccessTechnology != DataAccessTechnology.None)
      {
        _selectedTables.ForEach(t => tables.Add(t.Name));
        SendToGeneralOutputWindow("Generating Entity Framework model...");
        if (tables.Count > 0)
        {
          if (_dataAccessTechnology == DataAccessTechnology.EntityFramework5)
          {
            _currentEntityFrameworkVersion = ENTITY_FRAMEWORK_VERSION_5;
          }
          else if (_dataAccessTechnology == DataAccessTechnology.EntityFramework6)
          {
            _currentEntityFrameworkVersion = ENTITY_FRAMEWORK_VERSION_6;
          }

          if (string.IsNullOrEmpty(_projectPath))
          {
            _projectPath = System.IO.Path.GetDirectoryName(project.FullName);
          }

          string modelPath = Path.Combine(_projectPath, "Models");
          ItemTemplateUtilities.GenerateEntityFrameworkModel(project, vsProj, new MySqlConnection(_connectionString), _selectedModel, tables,
              modelPath, "1", _language, ColumnMappings, ref TablesIncludedInModel);
          GenerateMVCItems(vsProj);
        }
      }

      SendToGeneralOutputWindow("Finished MVC item generation.");
    }

    /// <summary>
    /// Indicates whether the specified project item should be added to the project.
    /// </summary>
    /// <param name="filePath">The path to the project item.</param>
    /// <returns>
    /// true if the project item should be added to the project; otherwise, false.
    /// </returns>
    public bool ShouldAddProjectItem(string filePath)
    {
      return true;
    }
    #endregion

    /// <summary>
    /// Constructor. Initializes a new instance of the <see cref="ItemTemplatesBaseWebWizard "/> class.
    /// </summary>
    /// <param name="language">The language generator (C# or VB .NET).</param>
    public ItemTemplatesBaseWebWizard(LanguageGenerator language)
    {
      _language = language;
      _projectType = ItemTemplateUtilities.ProjectWizardType.AspNetMVC;
      // get the general output window
      IVsOutputWindow outWindow = Package.GetGlobalService(typeof(SVsOutputWindow)) as IVsOutputWindow;
      Guid generalPaneGuid = VSConstants.GUID_OutWindowGeneralPane;
      if (outWindow != null)
      {
        outWindow.CreatePane(ref generalPaneGuid, "General", 1, 0);
        outWindow.GetPane(ref generalPaneGuid, out _generalPane);
        _generalPane.Activate();
      }
    }

    /// <summary>
    /// Gets the form values from the wizard, and set them into the base private fields.
    /// </summary>
    /// <param name="form">The wizard form.</param>
    private void GetFormValues(ItemTemplatesWebWizard form)
    {
      _selectedModel = form.SelectedModel;
      _selectedEntity = form.SelectedEntity;
      _selectedTables = form.SelectedTables;
      _dataAccessTechnology = form.DataAccessTechnology;
    }

    /// <summary>
    /// Sends a message to the general output window.
    /// </summary>
    /// <param name="message">The message.</param>
    protected void SendToGeneralOutputWindow(string message)
    {
      if (_generalPane != null)
      {
        _generalPane.OutputString(Environment.NewLine + message);
      }
    }

    /// <summary>
    /// Creates the MVC item and add it to the MVC project.
    /// </summary>
    /// <param name="vsProj">The Visual Studio project.</param>
    private void GenerateMVCItems(VSProject vsProj)
    {
      if (string.IsNullOrEmpty(_connectionString))
      {
        return;
      }

      if (_selectedTables == null || _selectedTables.Count == 0)
      {
        return;
      }

#if CLR4 || NET_40_OR_GREATER
      IServiceProvider serviceProvider = new Microsoft.VisualStudio.Shell.ServiceProvider((Microsoft.VisualStudio.OLE.Interop.IServiceProvider)dte);
      Microsoft.VisualStudio.TextTemplating.VSHost.ITextTemplating t4 = serviceProvider.GetService(typeof(STextTemplating)) as ITextTemplating;
      ITextTemplatingSessionHost sessionHost = t4 as ITextTemplatingSessionHost;
      var controllerClassPath = string.Empty;
      var IndexFilePath = string.Empty;
      var fileExtension = string.Empty;
      Version productVersion = Assembly.GetExecutingAssembly().GetName().Version;
      var version = String.Format("{0}.{1}.{2}", productVersion.Major, productVersion.Minor, productVersion.Build);
      double visualStudioVersion;
      double.TryParse(ItemTemplateUtilities.GetVisualStudioVersion(dte), out visualStudioVersion);
      if (_language == LanguageGenerator.CSharp)
      {
        controllerClassPath = Path.GetFullPath(string.Format("{0}{1}{2}", T4Templates_Path, version, cSharpControllerClass_FileName));
        IndexFilePath = Path.GetFullPath(string.Format("{0}{1}{2}", T4Templates_Path, version, cSharpIndexFile_FileName));
        fileExtension = "cs";
      }
      else
      {
        controllerClassPath = Path.GetFullPath(string.Format("{0}{1}{2}", T4Templates_Path, version, vbControllerClass_FileName));
        IndexFilePath = Path.GetFullPath(string.Format("{0}{1}{2}", T4Templates_Path, version, vbIndexFile_FileName));
        fileExtension = "vb";
      }

      StringBuilder catalogs = new StringBuilder();
      catalogs = new StringBuilder("<h3> Catalog list</h3>");
      catalogs.AppendLine();

      foreach (var table in TablesIncludedInModel)
      {
        catalogs.AppendLine(string.Format(@"<div> @Html.ActionLink(""{0}"",""Index"", ""{0}"")</div>",
                              table.Key[0].ToString().ToUpperInvariant() + table.Key.Substring(1)));
      }

      try
      {
        foreach (var table in TablesIncludedInModel)
        {
          // creating controller file
          sessionHost.Session = sessionHost.CreateSession();
          sessionHost.Session["namespaceParameter"] = string.Format("{0}.Controllers", _projectNamespace);
          sessionHost.Session["applicationNamespaceParameter"] = string.Format("{0}.Models", _projectNamespace);
          sessionHost.Session["controllerClassParameter"] = string.Format("{0}Controller", table.Key[0].ToString().ToUpperInvariant() + table.Key.Substring(1));
          if ((_dataAccessTechnology == DataAccessTechnology.EntityFramework6 && _language == LanguageGenerator.VBNET)
                || _language == LanguageGenerator.CSharp)
          {
            sessionHost.Session["modelNameParameter"] = _connectionName;
          }
          else if (_dataAccessTechnology == DataAccessTechnology.EntityFramework5 && _language == LanguageGenerator.VBNET)
          {
            sessionHost.Session["modelNameParameter"] = string.Format("{1}.{0}", _connectionName, _projectNamespace);
          }

          sessionHost.Session["classNameParameter"] = table.Key;
          sessionHost.Session["entityNameParameter"] = table.Key[0].ToString().ToUpperInvariant() + table.Key.Substring(1);
          sessionHost.Session["entityClassNameParameter"] = table.Key;
          if ((visualStudioVersion < 12.0 && _language == LanguageGenerator.VBNET)
                || _language == LanguageGenerator.CSharp)
          {
            sessionHost.Session["entityClassNameParameterWithNamespace"] = string.Format("{0}.{1}", _projectNamespace, table.Key);
          }
          else if (_language == LanguageGenerator.VBNET && visualStudioVersion >= 12.0)
          {
            if (_dataAccessTechnology == DataAccessTechnology.EntityFramework5)
            {
              sessionHost.Session["entityClassNameParameterWithNamespace"] = string.Format("{0}.{0}.{1}", _projectNamespace, table.Key);
            }
            else if (_dataAccessTechnology == DataAccessTechnology.EntityFramework6)
            {
              sessionHost.Session["entityClassNameParameterWithNamespace"] = string.Format("{0}.{1}", _projectNamespace, table.Key);
            }
          }

          T4Callback cb = new T4Callback();
          StringBuilder resultControllerFile = new StringBuilder(t4.ProcessTemplate(controllerClassPath, File.ReadAllText(controllerClassPath), cb));
          string controllerFilePath = string.Format(@"{0}\Controllers\{1}Controller.{2}", _projectPath,
                                                      table.Key[0].ToString().ToUpperInvariant() + table.Key.Substring(1), fileExtension);
          File.WriteAllText(controllerFilePath, resultControllerFile.ToString());
          if (cb.errorMessages.Count > 0)
          {
            File.AppendAllLines(controllerFilePath, cb.errorMessages);
          }

          vsProj.Project.ProjectItems.AddFromFile(controllerFilePath);
          var viewPath = Path.GetFullPath(_projectPath + string.Format(@"\Views\{0}", table.Key[0].ToString().ToUpperInvariant() + table.Key.Substring(1)));
          Directory.CreateDirectory(viewPath);
          string resultViewFile = t4.ProcessTemplate(IndexFilePath, File.ReadAllText(IndexFilePath), cb);
          File.WriteAllText(string.Format(viewPath + @"\Index.{0}html", fileExtension), resultViewFile);
          if (cb.errorMessages.Count > 0)
          {
            File.AppendAllLines(controllerFilePath, cb.errorMessages);
          }

          vsProj.Project.ProjectItems.AddFromFile(string.Format(viewPath + @"\Index.{0}html", fileExtension));
        }
      }
      catch (Exception ex)
      {
        SendToGeneralOutputWindow(string.Format("An error occurred: {0}\n\n {1}", ex.Message, ex.StackTrace));
        InfoDialog.ShowDialog(InfoDialogProperties.GetErrorDialogProperties(Resources.ErrorTitle, Resources.ItemTemplatesBaseWebWizard_GenerateMvcItemsError));
      }
#endif
    }
  }
}
