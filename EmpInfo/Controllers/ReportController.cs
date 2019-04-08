﻿using System;
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

        #region 请假流程报表

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
                         //join dl in db.ei_department.Where(d => d.FNumber.StartsWith(dep.FNumber)) on v.dep_no equals dl.FNumber
                         where
                         v.dep_no.StartsWith(dep.FNumber)
                         && v.from_date <= toDate && v.to_date >= fromDate
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

            //排序由请假日期改为申请日期
            var list = datas.OrderBy(m => m.apply_time).Skip((currentPage - 1) * pageSize).Take(pageSize).ToList();
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
            //排序改为按照提交日期，2018-11-27;2019-01-02,本来是在数据库排序，改为在服务器内存中排序，大大提高了查询速度，并解决了死锁问题。
            var myData = GetALDatas(depId, fromDate, toDate, auditStatus, eLevel, empName, sysNum, salaryNo).ToList().OrderBy(m => m.apply_time).ToList();
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
                cells.Add(rowIndex, ++colIndex, d.current_auditors);
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

        [SessionTimeOutFilter]
        public void BeginExportALAuditedExcel(string fromDate, string toDate, string sysNo = "", string cardNo = "")
        {
            DateTime fromDateDt, toDateDt;
            if (!DateTime.TryParse(fromDate, out fromDateDt)) {
                fromDateDt = DateTime.Now.AddMonths(-1);
            }
            if (!DateTime.TryParse(toDate, out toDateDt)) {
                toDateDt = DateTime.Now;
            }

            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            var myData = new List<FlowAuditListModel>();
            
            myData = flow.GetAuditList(userInfo.cardNo, sysNo, cardNo, "", "", fromDateDt.ToShortDateString(), toDateDt.ToShortDateString(), new ArrayOfInt() { 1, -1 }, new ArrayOfInt() { 10 }, new ArrayOfString() { "AL" }, 300).ToList();
            myData.ForEach(l => l.applier = GetUserNameByCardNum(l.applier));
            
            WriteEventLog("已办Excel", "导出请假已办记录");

            ushort[] colWidth = new ushort[] { 20, 16, 40, 12, 18, 16 };

            string[] colName = new string[] { "申请人", "申请时间", "请假时间", "类型", "流水号", "审批结果" };

            //設置excel文件名和sheet名
            XlsDocument xls = new XlsDocument();
            xls.FileName = "请假已办列表";
            Worksheet sheet = xls.Workbook.Worksheets.Add("已办数据列表");

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

                //"申请人", "申请时间", "请假时间", "类型", "流水号", "审批结果"
                cells.Add(++rowIndex, colIndex, d.applier);
                cells.Add(rowIndex, ++colIndex, ((DateTime)d.applyTime).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, d.subTitle);
                cells.Add(rowIndex, ++colIndex, d.title);
                cells.Add(rowIndex, ++colIndex, d.sysNo);
                cells.Add(rowIndex, ++colIndex, d.auditResult);                
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

        #endregion

        #region 夜间出货

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

        [SessionTimeOutFilter]
        public ActionResult UCReport()
        {
            return View();
        }

        public void BeginExportUCReport(DateTime fromDate, DateTime toDate)
        {
            toDate = toDate.AddDays(1);
            var result = db.vw_UCExcel.Where(u => u.apply_time > fromDate && u.apply_time <= toDate).ToList();

            ushort[] colWidth = new ushort[] { 8, 16, 12, 16, 16, 16, 24, 12,
                                               24, 16, 24, 24, 12, 32 };

            string[] colName = new string[] { "序号", "申请流水号", "申请人", "申请时间", "完成申请时间","到达时间", "市场部", "出货公司",
                                              "客户", "生产事业部", "货运公司", "出货型号", "出货数量","申请原因" };

            //設置excel文件名和sheet名
            XlsDocument xls = new XlsDocument();
            xls.FileName = "夜间出货申请列表_" + DateTime.Now.ToString("MMddHHmmss");
            Worksheet sheet = xls.Workbook.Worksheets.Add("夜间出货详情");

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

                //"序号", "申请流水号", "申请人", "申请时间", "完成申请时间", "市场部", "出货公司"
                //"客户", "生产事业部", "货运公司", "出货型号", "出货数量","申请原因"
                cells.Add(++rowIndex, colIndex, rowIndex - 1);
                cells.Add(rowIndex, ++colIndex, d.sys_no);
                cells.Add(rowIndex, ++colIndex, d.applier_name);
                cells.Add(rowIndex, ++colIndex, ((DateTime)d.apply_time).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, ((DateTime)d.finish_date).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, ((DateTime)d.arrive_time).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, d.market_name);
                cells.Add(rowIndex, ++colIndex, d.company);

                cells.Add(rowIndex, ++colIndex, d.customer_name);
                cells.Add(rowIndex, ++colIndex, d.bus_dep);
                cells.Add(rowIndex, ++colIndex, d.delivery_company);
                cells.Add(rowIndex, ++colIndex, d.moduel);
                cells.Add(rowIndex, ++colIndex, d.qty);
                cells.Add(rowIndex, ++colIndex, d.reason);
            }

            xls.Send();
        }

        #endregion

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

        #region 电子调休流程

        [SessionTimeOutFilter]
        public ActionResult SVReport()
        {
            var sm = new SVSearchParam();
            var cookie = Request.Cookies["svReport"];
            if (cookie == null) {
                sm = new SVSearchParam()
                {
                    vFromDate = DateTime.Now.ToString("yyyy-MM-01"),
                    vToDate = DateTime.Now.ToString("yyyy-MM-dd"),
                    dFromDate = DateTime.Now.ToString("yyyy-MM-01"),
                    dToDate = DateTime.Now.ToString("yyyy-MM-dd"),
                    auditStatus = "所有"
                };                
            }
            else {
                sm = JsonConvert.DeserializeObject<SVSearchParam>(cookie.Values.Get("sm"));
                sm.auditStatus = MyUtils.DecodeToUTF8(sm.auditStatus);
                sm.empName = MyUtils.DecodeToUTF8(sm.empName);
                sm.depName = MyUtils.DecodeToUTF8(sm.depName);
                sm.salaryNo = string.IsNullOrEmpty(sm.salaryNo) ? "" : MyUtils.DecodeToUTF8(sm.salaryNo);
                sm.sysNum = string.IsNullOrEmpty(sm.sysNum) ? "" : MyUtils.DecodeToUTF8(sm.sysNum);
            }
            
            ViewData["sm"] = sm;
            return View();
        }

        private IQueryable<vw_svReport> GetSVDatas(int depId, DateTime vFromDate, DateTime vToDate,DateTime dFromDate,DateTime dToDate, string auditStatus, string empName = "", string salaryNo = "")
        {
            var dep = db.ei_department.Single(d => d.id == depId);
            empName = empName.Trim();
            salaryNo = salaryNo.Trim();

            //保存到cookie
            var sm = new SVSearchParam()
            {
                depId = depId.ToString(),
                depName = MyUtils.EncodeToUTF8(GetDepLongNameByNum(dep.FNumber)),
                vFromDate = vFromDate.ToString("yyyy-MM-dd"),
                vToDate = vToDate.ToString("yyyy-MM-dd"),
                dFromDate = dFromDate.ToString("yyyy-MM-dd"),
                dToDate = dToDate.ToString("yyyy-MM-dd"),
                auditStatus = MyUtils.EncodeToUTF8(auditStatus),
                empName = MyUtils.EncodeToUTF8(empName),
                salaryNo = MyUtils.EncodeToUTF8(salaryNo)
            };
            var cookie = new HttpCookie("svReport");
            cookie.Values.Add("sm", JsonConvert.SerializeObject(sm));
            cookie.Expires = DateTime.Now.AddDays(7);
            Response.AppendCookie(cookie);

            vToDate = vToDate.AddDays(1);
            dToDate = dToDate.AddDays(1);

            var myData = from v in db.vw_svReport
                         //join dl in db.ei_department.Where(d => d.FNumber.StartsWith(dep.FNumber)) on v.dep_no equals dl.FNumber
                         where
                         v.dep_no.StartsWith(dep.FNumber)
                         && v.vacation_date_from <= vToDate && v.vacation_date_to >= vFromDate
                         && v.duty_date_from <= dToDate && v.duty_date_to >= dFromDate
                         && (auditStatus == "所有" || v.status == auditStatus)
                         select v;

            if (!string.IsNullOrEmpty(empName)) {
                myData = myData.Where(m => m.applier_name == empName);
            }            
            if (!string.IsNullOrEmpty(salaryNo)) {
                myData = myData.Where(m => m.salary_no == salaryNo);
            }

            WriteEventLog("调休报表", "开始查询");
            return myData;
        }

        [SessionTimeOutFilter]
        public ActionResult CheckSVDatas(int depId, DateTime vFromDate, DateTime vToDate, DateTime dFromDate, DateTime dToDate, string auditStatus, string empName = "", string salaryNo = "", int currentPage = 1)
        {
            int pageSize = 100;
            var datas = GetSVDatas(depId, vFromDate, vToDate,dFromDate,dToDate, auditStatus, empName, salaryNo);
            int totalRecord = datas.Count();
            var totalPage = Math.Ceiling((totalRecord * 1.0) / pageSize);
            
            var list = datas.OrderBy(m => m.apply_time).Skip((currentPage - 1) * pageSize).Take(pageSize).ToList();
            var dep = db.ei_department.Single(d => d.id == depId);

            var sm = new SVSearchParam()
            {
                depId = depId.ToString(),
                depName = GetDepLongNameByNum(dep.FNumber),
                vFromDate = vFromDate.ToString("yyyy-MM-dd"),
                vToDate = vToDate.ToString("yyyy-MM-dd"),
                dFromDate = dFromDate.ToString("yyyy-MM-dd"),
                dToDate = dToDate.ToString("yyyy-MM-dd"),
                auditStatus = auditStatus,
                empName = empName,
                salaryNo = salaryNo
            };

            ViewData["sm"] = sm;
            ViewData["list"] = list;
            ViewData["currentPage"] = currentPage;
            ViewData["totalPage"] = totalPage;

            return View("SVReport");
        }

        [SessionTimeOutFilter]
        public void BeginExportSVExcel(int depId, DateTime vFromDate, DateTime vToDate, DateTime dFromDate, DateTime dToDate, string auditStatus, string empName = "", string salaryNo = "")
        {
            var myData = GetSVDatas(depId, vFromDate, vToDate, dFromDate, dToDate, auditStatus, empName, salaryNo).OrderBy(m => m.apply_time).ToList();
            var dep = db.ei_department.Single(d => d.id == depId);

            ushort[] colWidth = new ushort[] { 10, 20, 12, 16, 60, 18, 18,
                                               18, 18, 24, 20, 24, 10, 10 };

            string[] colName = new string[] { "状态", "申请人", "条形码", "申请时间", "部门",  "值班开始日期", "值班结束日期", 
                                              "调休开始日期", "调休结束日期", "原因", "流水号","当前审核人","标志1","标志2" };

            //設置excel文件名和sheet名
            XlsDocument xls = new XlsDocument();
            xls.FileName = dep.FName + "调休信息";
            Worksheet sheet = xls.Workbook.Worksheets.Add("调休数据列表");

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

                //"状态", "申请人", "条形码", "申请时间", "部门",  "值班开始日期", "值班结束日期"
                //"调休开始日期", "调休结束日期", "原因", "流水号","当前审核人","标志1","标志2"
                cells.Add(++rowIndex, colIndex, d.status);
                cells.Add(rowIndex, ++colIndex, d.applier_name + "(" + d.applier_num + ")");
                cells.Add(rowIndex, ++colIndex, d.salary_no);
                cells.Add(rowIndex, ++colIndex, ((DateTime)d.apply_time).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, d.dep_long_name);
                cells.Add(rowIndex, ++colIndex, ((DateTime)d.duty_date_from).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, ((DateTime)d.duty_date_to).ToString("yyyy-MM-dd HH:mm"));

                cells.Add(rowIndex, ++colIndex, ((DateTime)d.vacation_date_from).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, ((DateTime)d.vacation_date_to).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, d.reason);
                cells.Add(rowIndex, ++colIndex, d.sys_no);
                cells.Add(rowIndex, ++colIndex, GetUserNameAndCardByCardNum(d.current_auditors));
                cells.Add(rowIndex, ++colIndex, d.check1 == true ? "Y" : "");
                cells.Add(rowIndex, ++colIndex, d.check2 == true ? "Y" : "");
            }
            xls.Send();
        }

        [SessionTimeOutJsonFilter]
        public JsonResult SetSVFlag(string sysNo, int which, bool isChecked)
        {

            var al = db.ei_SVApply.Single(a => a.sys_no == sysNo);
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

            WriteEventLog("调休报表:" + sysNo, "设置标志：" + which + ":" + isChecked);
            return Json(new SimpleResultModel() { suc = true });
        }
                

        #endregion

        #region 电子漏刷卡流程

        [SessionTimeOutFilter]
        public ActionResult CRReport()
        {
            var sm = new CRSearchParam();
            var cookie = Request.Cookies["crReport"];
            if (cookie == null) {
                sm = new CRSearchParam()
                {
                    fromDate = DateTime.Now.ToString("yyyy-MM-01"),
                    toDate = DateTime.Now.ToString("yyyy-MM-dd"),
                    auditStatus = "所有"
                };
            }
            else {
                sm = JsonConvert.DeserializeObject<CRSearchParam>(cookie.Values.Get("sm"));
                sm.auditStatus = MyUtils.DecodeToUTF8(sm.auditStatus);
                sm.empName = MyUtils.DecodeToUTF8(sm.empName);
                sm.depName = MyUtils.DecodeToUTF8(sm.depName);
                sm.salaryNo = string.IsNullOrEmpty(sm.salaryNo) ? "" : MyUtils.DecodeToUTF8(sm.salaryNo);
                sm.sysNum = string.IsNullOrEmpty(sm.sysNum) ? "" : MyUtils.DecodeToUTF8(sm.sysNum);
            }
            
            ViewData["sm"] = sm;
            return View();
        }

        private IQueryable<vw_crReport> GetCRDatas(int depId, DateTime fromDate, DateTime toDate, string auditStatus, string empName = "", string salaryNo = "")
        {
            var dep = db.ei_department.Single(d => d.id == depId);
            empName = empName.Trim();
            salaryNo = salaryNo.Trim();

            //保存到cookie
            var sm = new CRSearchParam()
            {
                depId = depId.ToString(),
                depName = MyUtils.EncodeToUTF8(GetDepLongNameByNum(dep.FNumber)),
                fromDate = fromDate.ToString("yyyy-MM-dd"),
                toDate = toDate.ToString("yyyy-MM-dd"),
                auditStatus = MyUtils.EncodeToUTF8(auditStatus),
                empName = MyUtils.EncodeToUTF8(empName),
                salaryNo = MyUtils.EncodeToUTF8(salaryNo)
            };
            var cookie = new HttpCookie("crReport");
            cookie.Values.Add("sm", JsonConvert.SerializeObject(sm));
            cookie.Expires = DateTime.Now.AddDays(7);
            Response.AppendCookie(cookie);

            toDate = toDate.AddDays(1);

            var myData = from v in db.vw_crReport
                         //join dl in db.ei_department.Where(d => d.FNumber.StartsWith(dep.FNumber)) on v.dep_no equals dl.FNumber
                         where
                         v.dep_no.StartsWith(dep.FNumber)
                         && v.forgot_date <= toDate && v.forgot_date >= fromDate
                         && (auditStatus == "所有" || v.status == auditStatus)
                         select v;

            if (!string.IsNullOrEmpty(empName)) {
                myData = myData.Where(m => m.applier_name == empName);
            }
            if (!string.IsNullOrEmpty(salaryNo)) {
                myData = myData.Where(m => m.salary_no == salaryNo);
            }

            WriteEventLog("漏刷卡报表", "开始查询");
            return myData;
        }

        [SessionTimeOutFilter]
        public ActionResult CheckCRDatas(int depId, DateTime fromDate, DateTime toDate, string auditStatus, string empName = "", string salaryNo = "", int currentPage = 1)
        {
            int pageSize = 100;
            var datas = GetCRDatas(depId, fromDate, toDate, auditStatus, empName, salaryNo);
            int totalRecord = datas.Count();
            var totalPage = Math.Ceiling((totalRecord * 1.0) / pageSize);

            var list = datas.OrderBy(m => m.apply_time).Skip((currentPage - 1) * pageSize).Take(pageSize).ToList();
            var dep = db.ei_department.Single(d => d.id == depId);

            var sm = new CRSearchParam()
            {
                depId = depId.ToString(),
                depName = GetDepLongNameByNum(dep.FNumber),
                fromDate = fromDate.ToString("yyyy-MM-dd"),
                toDate = toDate.ToString("yyyy-MM-dd"),
                auditStatus = auditStatus,
                empName = empName,
                salaryNo = salaryNo
            };

            ViewData["sm"] = sm;
            ViewData["list"] = list;
            ViewData["currentPage"] = currentPage;
            ViewData["totalPage"] = totalPage;

            return View("CRReport");
        }

        [SessionTimeOutFilter]
        public void BeginExportCRExcel(int depId, DateTime fromDate, DateTime toDate, string auditStatus, string empName = "", string salaryNo = "")
        {
            var myData = GetCRDatas(depId, fromDate, toDate, auditStatus, empName, salaryNo).OrderBy(m => m.apply_time).ToList();
            var dep = db.ei_department.Single(d => d.id == depId);

            ushort[] colWidth = new ushort[] { 10, 20, 12, 16, 60, 12, 18,
                                               14, 24, 20, 24,10,10 };

            string[] colName = new string[] { "状态", "申请人", "条形码", "申请时间", "部门",  "漏刷日期", "漏刷时间", 
                                              "原因", "备注", "流水号","当前审核人","标志1","标志2" };

            //設置excel文件名和sheet名
            XlsDocument xls = new XlsDocument();
            xls.FileName = dep.FName + "考勤补记信息";
            Worksheet sheet = xls.Workbook.Worksheets.Add("考勤补记数据列表");

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

                //"状态", "申请人", "条形码", "申请时间", "部门",  "漏刷日期", "漏刷时间", 
                //"原因", "备注", "流水号","当前审核人","标志1","标志2"
                cells.Add(++rowIndex, colIndex, d.status);
                cells.Add(rowIndex, ++colIndex, d.applier_name + "(" + d.applier_num + ")");
                cells.Add(rowIndex, ++colIndex, d.salary_no);
                cells.Add(rowIndex, ++colIndex, ((DateTime)d.apply_time).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, d.dep_long_name);
                cells.Add(rowIndex, ++colIndex, ((DateTime)d.forgot_date).ToString("yyyy-MM-dd"));
                cells.Add(rowIndex, ++colIndex, d.forgot_time);

                cells.Add(rowIndex, ++colIndex, d.reason);
                cells.Add(rowIndex, ++colIndex, d.comment);
                cells.Add(rowIndex, ++colIndex, d.sys_no);
                cells.Add(rowIndex, ++colIndex, GetUserNameAndCardByCardNum(d.current_auditors));
                cells.Add(rowIndex, ++colIndex, d.check1 == true ? "Y" : "");
                cells.Add(rowIndex, ++colIndex, d.check2 == true ? "Y" : "");
            }
            xls.Send();
        }

        [SessionTimeOutJsonFilter]
        public JsonResult SetCRFlag(string sysNo, int which, bool isChecked)
        {

            var al = db.ei_CRApply.Single(a => a.sys_no == sysNo);
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

            WriteEventLog("漏刷卡报表:" + sysNo, "设置标志：" + which + ":" + isChecked);
            return Json(new SimpleResultModel() { suc = true });
        }

        [SessionTimeOutJsonFilter]
        public JsonResult UpdateCRDays(string sysNo, string forgotDate, string time1, string time2, string time3, string time4)
        {
            DateTime forgotDateDt;
            if (!DateTime.TryParse(forgotDate, out forgotDateDt)) {
                return Json(new SimpleResultModel() { suc = false, msg = "漏刷日期不符合日期格式" });
            }
            var cr = db.ei_CRApply.Single(c => c.sys_no == sysNo);

            WriteEventLog("漏刷卡修改时间", string.Format("流水号【{0}】,日期【{1:yyyy-MM-dd}】,时间【2】===>日期【{3}】,时间【{4}】"
                , sysNo
                , cr.forgot_date, cr.time1 + "," + cr.time2 + "," + cr.time3 + "," + cr.time4
                , forgotDate, time1 + "," + time2 + "," + time3 + "," + time4));

            try {
                cr.forgot_date = forgotDateDt;
                cr.time1 = time1;
                cr.time2 = time2;
                cr.time3 = time3;
                cr.time4 = time4;
                db.SaveChanges();
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel() { suc = false, msg = ex.Message });
            }
            return Json(new SimpleResultModel() { suc = true, msg = "保存成功" });
            
        }

        #endregion

        #region 设备保障维修流程

        [SessionTimeOutFilter]
        public ActionResult EPReport()
        {
            ViewData["searchParam"] = new EPSearchParam()
            {
                fromDate = DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd"),
                toDate = DateTime.Now.ToString("yyyy-MM-dd"),
                applyStatus = "所有",
                propertyNumber = "",
                procDepName = "",
                equitmentDepName = ""
            };

            return View();
        }

        [SessionTimeOutJsonFilter]
        public IQueryable<vw_epReport> GetEPDatas(DateTime fromDate, DateTime toDate, string applyStatus, string propertyNumber = "", string procDepName = "", string equitmentDepName = "")
        {
            toDate = toDate.AddDays(1);
            var data = from v in db.vw_epReport
                       where v.report_time >= fromDate
                       && v.report_time <= toDate
                       && (applyStatus == "所有"
                       || v.apply_status == applyStatus
                       )                       
                       select v;

            if (!string.IsNullOrEmpty(propertyNumber)) {
                data = data.Where(d => d.property_number.Contains(propertyNumber));
            }
            if (!string.IsNullOrEmpty(procDepName)) {
                data = data.Where(d => d.produce_dep_name.Contains(procDepName));
            }
            if (!string.IsNullOrEmpty(equitmentDepName)) {
                data = data.Where(d => d.equitment_dep_name.Contains(equitmentDepName));
            }
            
            //2019-3-22 事业部生产主管只能查询本部门的数据，结合另外一个表使用
            var busDeps = db.ei_epBusReportChecker.Where(e => e.checker_no == userInfo.cardNo).Select(e => e.bus_dep_name).Distinct().ToList();
            if (busDeps.Count() > 0) {
                data = data.Where(d => busDeps.Contains(d.bus_dep_name));
            }

            return data.OrderBy(d => d.report_time);
        }
        
        [SessionTimeOutFilter]
        public ActionResult CheckEPDatas(DateTime fromDate, DateTime toDate, string applyStatus, string propertyNumber = "", string procDepName = "", string equitmentDepName = "", int currentPage = 1)
        {
            int pageSize = 100;
            var datas = GetEPDatas(fromDate, toDate, applyStatus, propertyNumber, procDepName, equitmentDepName);
            int totalRecord = datas.Count();
            var totalPage = Math.Ceiling((totalRecord * 1.0) / pageSize);

            var list = datas.Skip((currentPage - 1) * pageSize).Take(pageSize).ToList();

            ViewData["list"] = list;
            ViewData["currentPage"] = currentPage;
            ViewData["totalPage"] = totalPage;
            ViewData["searchParam"] = new EPSearchParam()
            {
                fromDate = fromDate.ToString("yyyy-MM-dd"),
                toDate = toDate.ToString("yyyy-MM-dd"),
                applyStatus = applyStatus,
                propertyNumber = propertyNumber,
                procDepName = procDepName,
                equitmentDepName = equitmentDepName
            };

            return View("EPReport");
        }

        public void BeginExportEPExcel(DateTime fromDate, DateTime toDate, string applyStatus, string propertyNumber = "", string procDepName = "", string equitmentDepName = "")
        {
            var myData = GetEPDatas(fromDate, toDate, applyStatus, propertyNumber, procDepName, equitmentDepName).ToList();
            ushort[] colWidth = new ushort[] { 12, 16, 10, 10, 12, 18, 18, 12, 18, 24,
                                               16, 16, 16, 16, 16, 16, 16, 16,
                                               16, 16, 16, 16, 16, 16, 16, 16, 16, 16,
                                               16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16 };

            string[] colName = new string[] { "状态", "申请流水号", "报修日期", "报修时间", "报修人", "联系电话", "设备支部", "事业部", "车间名称", "岗位位置", 
                                              "设备名称", "设备型号", "固定资产类别", "固定资产编号","设备供应商","生产主管","设备经理","影响停产程度",
                                              "故障现象", "接单时间","接单人", "延迟处理原因","处理登记时间","处理完成时间","维修用时(分)", "维修人员","故障原因类别","故障原因",
                                              "处理方法","更换配件","修理结果","协助人员","评价时间","评价生产主管","评价打分","评价内容","评分时间","评分设备经理","难度分数" };                                              

            //設置excel文件名和sheet名
            XlsDocument xls = new XlsDocument();
            xls.FileName = "设备故障报修信息";
            Worksheet sheet = xls.Workbook.Worksheets.Add("数据列表");

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

                //"状态", "申请流水号", "报修时间", "报修人", "联系电话", "事业部", "车间名称", "车间位置", 
                //"设备名称", "设备型号", "资产编号","设备供应商","生产主管","设备经理","紧急处理级别",
                //"故障现象", "接单时间","接单人","处理完成时间","维修人员","故障原因类别","故障原因",
                //"处理方法","更换配件","修理结果","其他维修人员","评价时间","评价生产主管","评价打分",
                //"评价内容"
                cells.Add(++rowIndex, colIndex, d.apply_status);
                cells.Add(rowIndex, ++colIndex, d.sys_no);
                cells.Add(rowIndex, ++colIndex, ((DateTime)d.report_time).ToString("yyyy-MM-dd"));
                cells.Add(rowIndex, ++colIndex, ((DateTime)d.report_time).ToString("HH:mm"));
                cells.Add(rowIndex, ++colIndex, d.applier_name);
                cells.Add(rowIndex, ++colIndex, d.applier_phone);
                cells.Add(rowIndex, ++colIndex, d.equitment_dep_name);
                cells.Add(rowIndex, ++colIndex, d.bus_dep_name);
                cells.Add(rowIndex, ++colIndex, d.produce_dep_name);
                cells.Add(rowIndex, ++colIndex, d.produce_dep_addr);

                cells.Add(rowIndex, ++colIndex, d.equitment_name);
                cells.Add(rowIndex, ++colIndex, d.equitment_modual);
                cells.Add(rowIndex, ++colIndex, d.property_type);
                cells.Add(rowIndex, ++colIndex, d.property_number);
                cells.Add(rowIndex, ++colIndex, d.equitment_supplier);
                cells.Add(rowIndex, ++colIndex, d.produce_dep_charger_name);
                cells.Add(rowIndex, ++colIndex, d.equitment_dep_charger_name);
                cells.Add(rowIndex, ++colIndex, ((EmergencyEnum)d.emergency_level).ToString());

                cells.Add(rowIndex, ++colIndex, d.faulty_situation);
                cells.Add(rowIndex, ++colIndex, d.accept_time==null?"":((DateTime)d.accept_time).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, d.accept_user_name);
                cells.Add(rowIndex, ++colIndex, d.confirm_later_reason);
                cells.Add(rowIndex, ++colIndex, d.confirm_register_time == null ? "" : ((DateTime)d.confirm_register_time).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, d.confirm_time == null ? "" : ((DateTime)d.confirm_time).ToString("yyyy-MM-dd HH:mm"));
                if (d.confirm_time == null || d.confirm_register_time == null) {
                    cells.Add(rowIndex, ++colIndex, "");
                }
                else {
                    var confirmTime = DateTime.Parse(((DateTime)d.confirm_time).ToString("yyyy-MM-dd HH:mm"));
                    var reportTime = DateTime.Parse(((DateTime)d.report_time).ToString("yyyy-MM-dd HH:mm"));
                    cells.Add(rowIndex, ++colIndex, (confirmTime - reportTime).TotalMinutes);
                }
                cells.Add(rowIndex, ++colIndex, d.confirm_user_name);
                cells.Add(rowIndex, ++colIndex, d.faulty_type);
                cells.Add(rowIndex, ++colIndex, d.faulty_reason);

                cells.Add(rowIndex, ++colIndex, d.repair_method);
                cells.Add(rowIndex, ++colIndex, d.exchange_parts);
                cells.Add(rowIndex, ++colIndex, d.repair_result);
                cells.Add(rowIndex, ++colIndex, d.other_repairers);
                cells.Add(rowIndex, ++colIndex, d.evaluation_time == null ? "" : ((DateTime)d.evaluation_time).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, d.produce_dep_charger_name);
                cells.Add(rowIndex, ++colIndex, d.evaluation_score == 2 ? "满意" : (d.evaluation_score == 1 ? "一般" : (d.evaluation_score == 0 ? "不满意" : "")));
                cells.Add(rowIndex, ++colIndex, d.evaluation_content);
                cells.Add(rowIndex, ++colIndex, d.grade_time == null ? "" : ((DateTime)d.grade_time).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, d.equitment_dep_charger_name);
                cells.Add(rowIndex, ++colIndex, d.difficulty_score);
            }
            xls.Send();
        }

        #endregion

    }
}