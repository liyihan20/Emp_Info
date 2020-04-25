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
            SqlGenModel m = new SqlGenModel();
            MyUtils.SetFieldValueToModel(fc, m);

            if (string.IsNullOrWhiteSpace(m.serverName)) {
                return Json(new SimpleResultModel(false, "服务器地址不能为空"));
            }
            if (string.IsNullOrWhiteSpace(m.dbName)) {
                return Json(new SimpleResultModel(false, "数据库不能为空"));
            }
            if (string.IsNullOrWhiteSpace(m.dbLoginName)) {
                return Json(new SimpleResultModel(false, "用户名不能为空"));
            }
            if (string.IsNullOrWhiteSpace(m.sqlText)) {
                return Json(new SimpleResultModel(false, "SQL不能为空"));
            }

            string conString = string.Format("Data Source = {0};Initial Catalog = {1};Persist Security Info = True;User ID = {2};Password = {3}", m.serverName, m.dbName, m.dbLoginName, m.dbPassword);
            DataSet ds = null;
            SqlConnection conn = null;

            try {
                using (conn = new SqlConnection(conString)) {
                    conn.Open();
                    ds = new DataSet();
                    new SqlDataAdapter(m.sqlText, conn).Fill(ds);
                }
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel(ex));
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

            return Json(new { suc = true, columns = JsonConvert.SerializeObject(columns), rows = JsonConvert.SerializeObject(rows) });
        }

        public string ExecTest()
        {
            string conString = "Data Source = 192.168.100.205;Initial Catalog = ICAudit;Persist Security Info = True;User ID = ICEmp;Password = ICEmp12345";
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
