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
using EmpInfo.QywxWebSrv;

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

        public string TestApply()
        {
            //开始构造申请类
            WxApply ap = new WxApply();
            ap.creator_userid = "110428101"; //申请人id
            ap.template_id = "Bs7wQnouyV7bZW5b3exPDppXfjizqzFod9jpAtWpH"; //模板id
            ap.use_template_approver = 0; //审批人模式：0-通过接口指定审批人、抄送人（此时approver、notifyer等参数可用）; 1-使用此模板在管理后台设置的审批流程，支持条件审批。默认为0
            //设置审核人
            ap.approver = new WxApplyApprover[]{
                new WxApplyApprover(){
                    attr = 1, //节点审批方式：1-或签；2-会签，仅在节点为多人审批时有效
                    userid = new EmpInfo.QywxWebSrv.ArrayOfString(){"110428101"} //审批节点审批人userid列表，若为多人会签、多人或签，需填写每个人的userid
                }
            };
            //审批申请数据，可定义审批申请中各个控件的值，其中必填项必须有值，选填项可为空，数据结构同“获取审批申请详情”接口返回值中同名参数“apply_data” 
            ap.apply_data = new WxApplyData()
            {
                contents = new WxApplyDataContent[]
                {
                    new WxApplyDataContent(){
                        control = "Text", //控件类型：Text-文本；Textarea-多行文本；Number-数字；Money-金额；Date-日期/日期+时间；Selector-单选/多选；；Contact-成员/部门；Tips-说明文字；File-附件；Table-明细；
                        id = "Text-1594091379903", //控件id：控件的唯一id，可通过“获取审批模板详情”接口获取
                        value = new ControValue(){
                            text = "hello,world!" //文本控件的值
                        }
                    }
                }
            };
            //设置推送中显示的摘要
            ap.summary_list = new SummaryListModel[]{
                new SummaryListModel(){
                    summary_info = new SummaryInfoModel[]{
                        new SummaryInfoModel(){
                            text = "第一行推送摘要",
                            lang = "zh_CN"
                        }
                    }
                },
                new SummaryListModel(){
                    summary_info = new SummaryInfoModel[]{
                        new SummaryInfoModel(){
                            text = "第二行推送摘要",
                            lang = "zh_CN"
                        }
                    }
                }
            };
            string sp_no = ""; //成功时返回的表单编号
            try {
                //实例化web service
                sp_no = new QywxApiSrvSoapClient().WxBeginApply(ap); //开始调用web service接口
            }
            catch (Exception ex) {
                return ex.Message; //错误时提示信息
            }

            return sp_no;

        }

        

    }

    public class sqlResult
    {
        public decimal number { get; set; }
        public string name { get; set; }
        public string en_name { get; set; }
    }

    

}
