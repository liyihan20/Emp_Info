using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using EmpInfo.Models;
using System.Data;
using System.Data.SqlClient;
using Newtonsoft.Json;

namespace EmpInfo.Services
{
    public class BIBaseSv : BaseSv
    {
        public TableResultModel GetTableResult(string sqlText, ConnectionModel con = null)
        {
            string conString = "";
            if (con == null) {
                conString = db.Database.Connection.ConnectionString;
            }
            else {
                conString = string.Format("Data Source = {0};Initial Catalog = {1};Persist Security Info = True;User ID = {2};Password = {3}", con.serverName, con.dbName, con.dbLoginName, con.dbPassword);
            }

            DataSet ds = null;
            SqlConnection conn = null;

            using (conn = new SqlConnection(conString)) {
                conn.Open();
                ds = new DataSet();
                new SqlDataAdapter(sqlText, conn).Fill(ds);
            }

            var tb = ds.Tables[0];

            List<TableColumnModel> columns = new List<TableColumnModel>();
            for (var i = 0; i < tb.Columns.Count; i++) {
                columns.Add(new TableColumnModel(tb.Columns[i].ColumnName));
            }

            List<Dictionary<string, object>> rows = new List<Dictionary<string, object>>();
            Dictionary<string, object> dic;
            foreach (DataRow row in tb.Rows) {
                dic = new Dictionary<string, object>();
                foreach (var cn in columns) {
                    dic.Add(cn.field, row[cn.field]);
                }
                rows.Add(dic);
            }

            return new TableResultModel() { columns = JsonConvert.SerializeObject(columns), rows = JsonConvert.SerializeObject(rows) };

        }
    }
}