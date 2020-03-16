using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using EmpInfo.Services;
using EmpInfo.Models;
using EmpInfo.FlowSvr;
using Newtonsoft.Json;
using System.Data;
using System.Data.SqlClient;
using System.Configuration;

namespace EmpInfo.Controllers
{
    public class TestController : BaseController
    {
        public string UCMsg()
        {
            UCSv sv = new UCSv("UC19050701");
            sv.SendNotification(new FlowResultModel() { suc = true, msg = "完成" });
            return "ok";
        }

        public string SqlExec()
        {
            string conString = "Data Source=192.168.100.205;Initial Catalog=ICAudit;Persist Security Info=True;User ID=ICEmp;Password=ICEmp12345";
            var sql = "select number,name,en_name,(case when number > 4 then 'b' else 's' end) as c from dbo.dn_authority where number > {0}";
            DataSet ds = null;
            SqlConnection conn = null;
            
            using (conn = new SqlConnection(conString)) {
                conn.Open();
                ds = new DataSet();
                new SqlDataAdapter(string.Format(sql,2), conn).Fill(ds);
            }

            var tb = ds.Tables[0];
            string[] columnNames=new string[tb.Columns.Count];
            for (var i = 0; i < tb.Columns.Count;i++ ) {
                columnNames[i] = tb.Columns[i].ColumnName;
            }

            List<Dictionary<string, object>> rows = new List<Dictionary<string, object>>();
            Dictionary<string, object> dic;
            foreach (DataRow row in tb.Rows) {
                dic = new Dictionary<string, object>();
                foreach (var cn in columnNames) {
                    dic.Add(cn, row[cn]);
                }
                rows.Add(dic);
            }
            
            return JsonConvert.SerializeObject(rows.FirstOrDefault().Keys.ToList());
        }

        public ActionResult RichTextBox()
        {
            return View();
        }

    }

    public class sqlResult
    {
        public decimal number { get; set; }
        public string name { get; set; }
        public string en_name { get; set; }
    }

}
