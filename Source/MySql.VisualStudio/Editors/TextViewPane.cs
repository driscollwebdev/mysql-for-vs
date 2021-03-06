﻿// Copyright © 2015, Oracle and/or its affiliates. All rights reserved.
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
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using MySqlX;
using MySqlX.Shell;

namespace MySql.Data.VisualStudio.Editors
{
  public partial class TextViewPane : UserControl
  {
    /// <summary>
    /// Creates a new instance of TextViewPane
    /// </summary>
    public TextViewPane()
    {
      InitializeComponent();
    }

    /// <summary>
    /// Set the data received to the text area with a json format
    /// </summary>
    /// <param name="dictionaryList">List of dictionaries that returned the query.</param>
    public void SetData(List<Dictionary<string, object>> dictionaryList)
    {
      txtJsondata.AppendText(dictionaryList.ToJson());
    }

    /// <summary>
    /// Set the data received to the text area with a json format
    /// </summary>
    /// <param name="document"></param>
    public void SetData(DocResult document)
    {
      txtJsondata.AppendText(document.ToJson());
    }
  }
}
