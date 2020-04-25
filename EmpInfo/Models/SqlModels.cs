using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace EmpInfo.Models
{
    public class SqlGenModel
    {
        public string serverName { get; set; }
        public string dbName { get; set; }
        public string dbLoginName { get; set; }
        public string dbPassword { get; set; }
        public string sqlText { get; set; }
    }

    public class TableColumnModel
    {
        public TableColumnModel() { }
        public TableColumnModel(string fieldAndTitle)
        {
            this.field = fieldAndTitle;
            this.title = fieldAndTitle;
        }
        public TableColumnModel(string _field, string _title)
        {
            this.field = _field;
            this.title = _title;
        }
        public string field { get; set; }
        public string title { get; set; }
    }
}