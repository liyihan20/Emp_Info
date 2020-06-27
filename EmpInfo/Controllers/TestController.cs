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
using EmpInfo.Util;

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

        public ActionResult SqlTest()
        {
            return View();
        }

        public JsonResult SqlExec(FormCollection fc)
        {
            ConnectionModel m = new ConnectionModel();
            MyUtils.SetFieldValueToModel(fc, m);
            string sqlText = fc.Get("sqlText");

            if (string.IsNullOrWhiteSpace(m.serverName)) {
                return Json(new SimpleResultModel(false, "服务器地址不能为空"));
            }
            if (string.IsNullOrWhiteSpace(m.dbName)) {
                return Json(new SimpleResultModel(false, "数据库不能为空"));
            }
            if (string.IsNullOrWhiteSpace(m.dbLoginName)) {
                return Json(new SimpleResultModel(false, "用户名不能为空"));
            }
            if (string.IsNullOrWhiteSpace(sqlText)) {
                return Json(new SimpleResultModel(false, "SQL不能为空"));
            }

            ConnectionModel cm = new ConnectionModel() { serverName = m.serverName, dbName = m.dbName, dbLoginName = m.dbLoginName, dbPassword = m.dbPassword };
            try {
                var result = new BIBaseSv().GetTableResult(sqlText, cm);
                return Json(new { suc = true, columns = result.columns, rows = result.rows });
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel(ex));
            }
            
        }

        public string ExecTest()
        {
            string conString = db.Database.Connection.ConnectionString;
            var sql = "select number,name,en_name,(case when number > 4 then 'b' else 's' end) as [好的] from dbo.dn_authority where number > {0}";
            DataSet ds = null;
            SqlConnection conn = null;
            
            using (conn = new SqlConnection(conString)) {
                conn.Open();
                ds = new DataSet();
                new SqlDataAdapter(string.Format(sql,2), conn).Fill(ds);
            }

            var tb = ds.Tables[0];
            string[] columnNames = new string[tb.Columns.Count];
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



    }

    public class sqlResult
    {
        public decimal number { get; set; }
        public string name { get; set; }
        public string en_name { get; set; }
    }

}
