using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using EmpInfo.Models;
using EmpInfo.Filter;
using EmpInfo.Util;
using org.in2bits.MyXls;
using Newtonsoft.Json;
using System.Data.SqlClient;
using EmpInfo.FlowSvr;


namespace EmpInfo.Controllers
{
    public class ReportController : BaseController
    {

        [SessionTimeOutFilter]
        public ActionResult ALReport()
        {
            ALSearchParam sm;
            var cookie = Request.Cookies["alReport"];
            if (cookie == null) {
                sm = new ALSearchParam();
                sm.fromDate = DateTime.Now.AddDays(-7).ToString("yyyy-MM-dd");
                sm.toDate = DateTime.Now.ToString("yyyy-MM-dd");
                sm.auditStatus = "所有";
                sm.empLeve = -1;
            }
            else {
                sm = JsonConvert.DeserializeObject<ALSearchParam>(cookie.Values.Get("sm"));
                sm.auditStatus = MyUtils.DecodeToUTF8(sm.auditStatus);
                sm.empName = MyUtils.DecodeToUTF8(sm.empName);
                sm.depName = MyUtils.DecodeToUTF8(sm.depName);
                sm.salaryNo = string.IsNullOrEmpty(sm.salaryNo) ? "" : MyUtils.DecodeToUTF8(sm.salaryNo);
                sm.sysNum = string.IsNullOrEmpty(sm.sysNum) ? "" : MyUtils.DecodeToUTF8(sm.sysNum);
            }

            ViewData["sm"] = sm;
            ViewData["empLevels"] = db.ei_empLevel.OrderBy(m => m.level_no).ToList();
            return View();
        }

        private IQueryable<vw_askLeaveReport> GetALDatas(int depId, DateTime fromDate, DateTime toDate, string auditStatus, int eLevel, string empName = "", string sysNum = "", string salaryNo = "")
        {            
            var dep = db.ei_department.Single(d => d.id == depId);
            empName = empName.Trim();
            salaryNo = salaryNo.Trim();
            sysNum = sysNum.Trim();

            //保存到cookie
            var sm = new ALSearchParam()
            {
                depId = depId.ToString(),
                depName = MyUtils.EncodeToUTF8(GetDepLongNameByNum(dep.FNumber)),
                fromDate = fromDate.ToString("yyyy-MM-dd"),
                toDate = toDate.ToString("yyyy-MM-dd"),
                auditStatus = MyUtils.EncodeToUTF8(auditStatus),
                empLeve = eLevel,
                empName = MyUtils.EncodeToUTF8(empName),
                sysNum = MyUtils.EncodeToUTF8(sysNum),
                salaryNo = MyUtils.EncodeToUTF8(salaryNo)
            };
            var cookie = new HttpCookie("alReport");
            cookie.Values.Add("sm", JsonConvert.SerializeObject(sm));
            cookie.Expires = DateTime.Now.AddDays(7);
            Response.AppendCookie(cookie);

            toDate = toDate.AddDays(1);
            
            
            var myData = from v in db.vw_askLeaveReport
                         join dl in db.ei_department.Where(d => d.FNumber.StartsWith(dep.FNumber)) on v.dep_no equals dl.FNumber
                         where
                         v.from_date <= toDate && v.to_date >= fromDate
                         && (auditStatus == "所有" || v.status == auditStatus)
                         && (eLevel == -1 || v.emp_level == eLevel)
                         select v;

            if (!string.IsNullOrEmpty(empName)) {
                myData = myData.Where(m => m.applier_name == empName);
            }
            if (!string.IsNullOrEmpty(sysNum)) {
                myData = myData.Where(m => m.sys_no == sysNum);
            }
            if (!string.IsNullOrEmpty(salaryNo)) {
                myData = myData.Where(m => m.salary_no == salaryNo);
            }

            WriteEventLog("请假报表", "开始查询：" + JsonConvert.SerializeObject(sm));
            return myData;
        }

        [SessionTimeOutFilter]
        public ActionResult CheckALDatas(int depId, DateTime fromDate, DateTime toDate, string auditStatus, int eLevel, string empName = "",string sysNum="",string salaryNo="",int currentPage = 1)
        {
            int pageSize = 100;
            var datas = GetALDatas(depId, fromDate, toDate, auditStatus, eLevel, empName, sysNum, salaryNo);
            int totalRecord = datas.Count();
            var totalPage = Math.Ceiling((totalRecord * 1.0) / pageSize);

            var list = datas.OrderBy(m => m.from_date).Skip((currentPage - 1) * pageSize).Take(pageSize).ToList();
            var dep = db.ei_department.Single(d => d.id == depId);

            var sm = new ALSearchParam()
            {
                depId = depId.ToString(),
                depName = GetDepLongNameByNum(dep.FNumber),
                fromDate = fromDate.ToString("yyyy-MM-dd"),
                toDate = toDate.ToString("yyyy-MM-dd"),
                auditStatus = auditStatus,
                empLeve = eLevel,
                empName = empName,
                salaryNo = salaryNo,
                sysNum = sysNum
            };

            ViewData["sm"] = sm;
            ViewData["list"] = list;
            ViewData["empLevels"] = db.ei_empLevel.OrderBy(m => m.level_no).ToList();
            ViewData["currentPage"] = currentPage;
            ViewData["totalPage"] = totalPage;
            
            return View("ALReport");
        }

        [SessionTimeOutFilter]
        public void BeginExportALExcel(int depId, DateTime fromDate, DateTime toDate, string auditStatus, int eLevel, string empName = "", string sysNum = "", string salaryNo = "")
        {
            var myData = GetALDatas(depId, fromDate, toDate, auditStatus, eLevel, empName, sysNum, salaryNo).OrderBy(m => m.from_date).ToList();
            var dep = db.ei_department.Single(d => d.id == depId);

            ushort[] colWidth = new ushort[] { 10, 20, 12, 16, 16, 60, 12, 10, 18, 18,
                                               10, 10, 12, 24, 20, 40, 16,24,10,10,36 };

            string[] colName = new string[] { "状态", "申请人", "条形码", "申请时间", "审批完成时间", "部门", "职位级别", "是否直管", "开始时间", "结束时间", 
                                              "天数", "小时数", "类型", "事由", "工作代理", "知会人", "流水号","当前审核人","标志1","标志2","查看链接" };

            //設置excel文件名和sheet名
            XlsDocument xls = new XlsDocument();
            xls.FileName = dep.FName+"请假信息";
            Worksheet sheet = xls.Workbook.Worksheets.Add("请假数据列表");

            //设置各种样式

            //标题样式
            XF boldXF = xls.NewXF();
            boldXF.HorizontalAlignment = HorizontalAlignments.Centered;
            boldXF.Font.Height = 12 * 20;
            boldXF.Font.FontName = "宋体";
            boldXF.Font.Bold = true;

            //设置列宽
            ColumnInfo col;
            for (ushort i = 0; i < colWidth.Length; i++) {
                col = new ColumnInfo(xls, sheet);
                col.ColumnIndexStart = i;
                col.ColumnIndexEnd = i;
                col.Width = (ushort)(colWidth[i] * 256);
                sheet.AddColumnInfo(col);
            }

            Cells cells = sheet.Cells;
            int rowIndex = 1;
            int colIndex = 1;

            //设置标题
            foreach (var name in colName) {
                cells.Add(rowIndex, colIndex++, name, boldXF);
            }

            foreach (var d in myData) {
                colIndex = 1;

                //"状态", "申请人", "申请时间", "部门", "职位级别", "是否直管", "请假开始时间", "请假结束时间", 
                //"请假天数", "请假小时数", "请假类型", "请假事由", "工作代理", "知会人","流水号", "附件"
                cells.Add(++rowIndex, colIndex, d.status);
                cells.Add(rowIndex, ++colIndex, d.applier_name + "(" + d.applier_num + ")");
                cells.Add(rowIndex, ++colIndex, d.salary_no);
                cells.Add(rowIndex, ++colIndex, ((DateTime)d.apply_time).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, d.finish_date == null ? "" : ((DateTime)d.finish_date).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, d.dep_long_name);
                cells.Add(rowIndex, ++colIndex, d.level_name);
                cells.Add(rowIndex, ++colIndex, d.is_direct_charge == true ? "是" : "否");
                cells.Add(rowIndex, ++colIndex, ((DateTime)d.from_date).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, ((DateTime)d.to_date).ToString("yyyy-MM-dd HH:mm"));

                cells.Add(rowIndex, ++colIndex, d.work_days);
                cells.Add(rowIndex, ++colIndex, d.work_hours);
                cells.Add(rowIndex, ++colIndex, d.leave_type);
                cells.Add(rowIndex, ++colIndex, d.leave_reason);
                cells.Add(rowIndex, ++colIndex, string.IsNullOrEmpty(d.agent_man)?"":(d.agent_man_name + "(" + d.agent_man + ")"));
                cells.Add(rowIndex, ++colIndex, string.IsNullOrEmpty(d.inform_man) ? "" : (d.inform_man_name + "(" + d.inform_man + ")"));
                cells.Add(rowIndex, ++colIndex, d.sys_no);
                cells.Add(rowIndex, ++colIndex, GetUserNameAndCardByCardNum(d.current_auditors));
                cells.Add(rowIndex, ++colIndex, d.check1 == true ? "Y" : "");
                cells.Add(rowIndex, ++colIndex, d.check2 == true ? "Y" : "");
                cells.Add(rowIndex, ++colIndex, string.Format("http://emp.truly.com.cn/Emp/Apply/CheckALApply?sysNo={0}", d.sys_no));
            }
            ++rowIndex;
            cells.Add(++rowIndex, 1, "备注：");
            cells.Add(rowIndex, 2, "真实的请假时间请以考勤记录为准");

            xls.Send();
        }

        [SessionTimeOutFilter]
        public void BeginExportALAuditingExcel()
        {
            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            var list = flow.GetAuditList(userInfo.cardNo, "", "", "", "", "", "", new ArrayOfInt() { 0 }, new ArrayOfInt() { 0 }, new ArrayOfString() { "AL" }, 100).ToList();
            var myData = (from l in list
                          join a in db.ei_askLeave on l.sysNo equals a.sys_no
                          join i in db.ei_leaveDayExceedPushLog on l.sysNo equals i.sys_no into ipl
                          join lv in db.ei_empLevel on a.emp_level equals lv.level_no
                          from pl in ipl.DefaultIfEmpty()
                          select new
                          {
                              al = a,
                              pushLog = pl,
                              empLevelName = lv.level_name
                          }).Distinct().ToList();

            WriteEventLog("待办Excel", "导出请假待办记录");

            ushort[] colWidth = new ushort[] { 20, 16, 60, 12, 18, 18,
                                               10, 10, 12, 24, 16, 16, 16, 16, 16};

            string[] colName = new string[] { "申请人", "申请时间", "部门", "职位级别", "开始时间", "结束时间", 
                                              "天数", "小时数", "类型", "事由", "流水号","面谈通知","预约时间","发送时间","发送人" };

            //設置excel文件名和sheet名
            XlsDocument xls = new XlsDocument();
            xls.FileName = "请假待办列表";
            Worksheet sheet = xls.Workbook.Worksheets.Add("待办数据列表");

            //设置各种样式

            //标题样式
            XF boldXF = xls.NewXF();
            boldXF.HorizontalAlignment = HorizontalAlignments.Centered;
            boldXF.Font.Height = 12 * 20;
            boldXF.Font.FontName = "宋体";
            boldXF.Font.Bold = true;

            //设置列宽
            ColumnInfo col;
            for (ushort i = 0; i < colWidth.Length; i++) {
                col = new ColumnInfo(xls, sheet);
                col.ColumnIndexStart = i;
                col.ColumnIndexEnd = i;
                col.Width = (ushort)(colWidth[i] * 256);
                sheet.AddColumnInfo(col);
            }

            Cells cells = sheet.Cells;
            int rowIndex = 1;
            int colIndex = 1;

            //设置标题
            foreach (var name in colName) {
                cells.Add(rowIndex, colIndex++, name, boldXF);
            }

            foreach (var d in myData) {
                colIndex = 1;                

                //"申请人", "申请时间", "部门", "职位级别", "开始时间", "结束时间", 
                //"天数", "小时数", "类型", "事由", "流水号","面谈通知","预约时间","发送时间"
                cells.Add(++rowIndex, colIndex, d.al.applier_name + "(" + d.al.applier_num + ")");                
                cells.Add(rowIndex, ++colIndex, ((DateTime)d.al.apply_time).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, d.al.dep_long_name);
                cells.Add(rowIndex, ++colIndex, d.empLevelName);
                cells.Add(rowIndex, ++colIndex, ((DateTime)d.al.from_date).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, ((DateTime)d.al.to_date).ToString("yyyy-MM-dd HH:mm"));

                cells.Add(rowIndex, ++colIndex, d.al.work_days);
                cells.Add(rowIndex, ++colIndex, d.al.work_hours);
                cells.Add(rowIndex, ++colIndex, d.al.leave_type);
                cells.Add(rowIndex, ++colIndex, d.al.leave_reason);
                cells.Add(rowIndex, ++colIndex, d.al.sys_no);
                cells.Add(rowIndex, ++colIndex, d.pushLog == null ? "未发送" : "已发送");
                if (d.pushLog != null) {
                    cells.Add(rowIndex, ++colIndex, ((DateTime)d.pushLog.book_date).ToString("yyyy-MM-dd HH:mm"));
                    cells.Add(rowIndex, ++colIndex, ((DateTime)d.pushLog.send_date).ToString("yyyy-MM-dd HH:mm"));
                    cells.Add(rowIndex, ++colIndex, d.pushLog.send_user);
                }
            }

            xls.Send();
        }

        [SessionTimeOutJsonFilter]
        public JsonResult SetALFlag(string sysNo,int which,bool isChecked){

            var al = db.ei_askLeave.Single(a => a.sys_no == sysNo);
            if (which == 1) {
                al.check1 = isChecked;
            }
            else if (which == 2) {
                al.check2 = isChecked;
            }
            else {
                return Json(new SimpleResultModel() { suc = false, msg = "参数错误" });
            }
            try {
                db.SaveChanges();
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel() { suc = false, msg = ex.Message });
            }

            WriteEventLog("请假报表:"+sysNo, "设置标志：" + which + ":" + isChecked);
            return Json(new SimpleResultModel() { suc = true });
        }

        [SessionTimeOutJsonFilter]
        public JsonResult UpdateALDays(string sysNo, string fromDate, string toDate, int days, decimal hours)
        {
            var al = db.ei_askLeave.Single(a => a.sys_no == sysNo);
            DateTime fromDateDt, toDateDt;

            //前台的日期不显示年，只显示月-日 时：分
            fromDate = ((DateTime)al.from_date).Year + "-" + fromDate;
            toDate = ((DateTime)al.to_date).Year + "-" + toDate;

            if (!DateTime.TryParse(fromDate, out fromDateDt)) {
                return Json(new SimpleResultModel() { suc = false, msg = "开始时间不合法" });
            }
            if (!DateTime.TryParse(toDate, out toDateDt)) {
                return Json(new SimpleResultModel() { suc = false, msg = "结束时间不合法" });
            }

            try {
                db.ei_ALModifyLog.Add(new ei_ALModifyLog()
                {
                    sys_no = al.sys_no,
                    old_begin_date = al.from_date,
                    old_end_date = al.to_date,
                    old_days = al.work_days,
                    old_hours = al.work_hours,
                    new_begin_date = fromDateDt,
                    new_end_date = toDateDt,
                    new_days = days,
                    new_hours = hours,
                    user_id = userInfo.id,
                    user_name = userInfo.name,
                    op_date = DateTime.Now,
                    ip = GetUserIP()
                });

                al.from_date = fromDateDt;
                al.to_date = toDateDt;
                al.work_days = days;
                al.work_hours = hours;

                db.SaveChanges();
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel() { suc = false, msg = ex.Message });
            }

            return Json(new SimpleResultModel() { suc = true, msg = "请假时间修改成功" });

        }

        [SessionTimeOutFilter]
        public ActionResult PrintUC(string sysNo)
        {
            var list = db.vw_UCReport.Where(v => v.sys_no == sysNo).ToList();
            if (list.Count() < 1) {
                ViewBag.tip = "单据不存在";
                return View("Error");
            }
            ViewData["list"] = list;

            WriteEventLog("非正常时间出货", "打印申请单：" + sysNo);
            return View();
        }

        #region 辅助、基干人员查询

        [AuthorityFilter]
        public ActionResult AssistantEmps()
        {
            return View();
        }

        public ActionResult SearchAssistantEmps(string value)
        {
            var list = db.vw_assistantEmps
                .Where(a => a.emp_name.Contains(value) || a.emp_no1.Contains(value))
                .Select(a => new AssistantEmpModel()
                {
                    depName = a.dept_name,
                    cardNumber = a.emp_no1,
                    empName = a.emp_name,
                    empType = a.emp_type
                }).ToList();

            ViewData["list"] = list;
            ViewData["searchValue"] = value;
            return View("AssistantEmps");
        }

        public ActionResult CheckAssistantEmp(string empType, string cardNumber)
        {
            var emp = db.vw_assistantEmps.Where(v => v.emp_type == empType && v.emp_no1 == cardNumber).FirstOrDefault();
            if (emp == null) {
                ViewBag.tip = "不存在";
                return View("Error");
            }
            ViewData["emp"] = emp;
            return View();
        }

        #endregion

        #region 导出出货系统审批记录

        public void ExportSRFlowRecord(string account, DateTime fromDate, DateTime toDate)
        {
            toDate = toDate.AddDays(1);
            var result = db.GetSRFlowRecord(account, fromDate, toDate).ToList();

            ushort[] colWidth = new ushort[] { 16, 16, 16, 30, 16, 16,
                                               32, 16, 16, 16, 32, 16,
                                               16, 16, 32, 16 };

            string[] colName = new string[] { "申请日期", "出货单号", "申请人", "客户名称", "办事处审核时间", "办事处审核人", 
                                              "办事处意见", "办事处审核结果", "总部审核时间", "总部审核人", "总部意见","总部审核结果",
                                              "会计部审核时间","会计部审核人","会计部意见","会计部审核结果" };

            //設置excel文件名和sheet名
            XlsDocument xls = new XlsDocument();
            xls.FileName = "出货审核意见_" + account + "_" + DateTime.Now.ToString("MMddHHmmss");
            Worksheet sheet = xls.Workbook.Worksheets.Add("出货审核意见列表");

            //设置各种样式

            //标题样式
            XF boldXF = xls.NewXF();
            boldXF.HorizontalAlignment = HorizontalAlignments.Centered;
            boldXF.Font.Height = 12 * 20;
            boldXF.Font.FontName = "宋体";
            boldXF.Font.Bold = true;

            //设置列宽
            ColumnInfo col;
            for (ushort i = 0; i < colWidth.Length; i++) {
                col = new ColumnInfo(xls, sheet);
                col.ColumnIndexStart = i;
                col.ColumnIndexEnd = i;
                col.Width = (ushort)(colWidth[i] * 256);
                sheet.AddColumnInfo(col);
            }

            Cells cells = sheet.Cells;
            int rowIndex = 1;
            int colIndex = 1;

            //设置标题
            foreach (var name in colName) {
                cells.Add(rowIndex, colIndex++, name, boldXF);
            }

            foreach (var d in result) {
                colIndex = 1;

                //"申请日期", "出货单号", "申请人", "客户名称", "办事处审核时间", "办事处审核人", 
                //"办事处意见", "办事处审核结果", "总部审核时间", "总部审核人", "总部意见","总部审核结果",
                //"会计部审核时间","会计部审核人","会计部意见","会计部审核结果"
                cells.Add(++rowIndex, colIndex, ((DateTime)d.FDate).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, d.FBillNo);
                cells.Add(rowIndex, ++colIndex, d.FApplier);
                cells.Add(rowIndex, ++colIndex, d.FCustomerName);
                cells.Add(rowIndex, ++colIndex, ((DateTime)d.FAgencyAuditDate).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, d.FAgencyAuditor);

                cells.Add(rowIndex, ++colIndex, d.FAgencyComment);
                cells.Add(rowIndex, ++colIndex, d.FAgencyAuditStatus);
                cells.Add(rowIndex, ++colIndex, ((DateTime)d.FMarketAuditDate).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, d.FMarketAuditor);
                cells.Add(rowIndex, ++colIndex, d.FMarketComment);
                cells.Add(rowIndex, ++colIndex, d.FMarketAuditStatus);
                
                cells.Add(rowIndex, ++colIndex, ((DateTime)d.FAccountAuditDate).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, d.FAccountAuditor);
                cells.Add(rowIndex, ++colIndex, d.FAccountComment);
                cells.Add(rowIndex, ++colIndex, d.FAccountAuditStatus);
            }

            xls.Send();

        }

        #endregion


    }
}
