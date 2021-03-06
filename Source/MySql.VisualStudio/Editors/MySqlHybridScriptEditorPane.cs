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

using System.Windows.Forms;
using Microsoft.VisualStudio.Shell;
using MySql.Utility.Enums;

namespace MySql.Data.VisualStudio.Editors
{
  class MySqlHybridScriptEditorPane : WindowPane
  {
    private readonly MySqlHybridScriptEditor _editor;
    internal SqlEditorFactory Factory { get; private set; }
    internal string DocumentPath { get; private set; }

    public MySqlHybridScriptEditorPane(ServiceProvider sp, SqlEditorFactory factory, ScriptLanguageType scriptType = ScriptLanguageType.JavaScript)
      : base(sp)
    {
      Factory = factory;
      DocumentPath = factory.LastDocumentPath;
      _editor = new MySqlHybridScriptEditor(sp, this, scriptType);
    }

    public override IWin32Window Window
    {
      get { return _editor; }
    }

    /// <summary>
    /// Overrides the Close event, to check whether the MySql Output should be closed as well.
    /// </summary>
    protected override void OnClose()
    {
      var package = MySqlDataProviderPackage.Instance;
      if (package != null)
      {
        package.CloseMySqlOutputWindow();
      }

      base.OnClose();
    }
  }
}
