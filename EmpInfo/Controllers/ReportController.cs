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
using EmpInfo.Services;


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
                cells.Add(rowIndex, ++colIndex, string.Format("http://emp.truly.com.cn/Emp/Apply/CheckApply?sysNo={0}", d.sys_no));
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

            ushort[] colWidth = new ushort[] { 12, 8, 16, 12, 16, 16, 16, 24, 12,
                                               24, 16, 24, 24, 12, 32 };

            string[] colName = new string[] { "处理状态", "序号", "申请流水号", "申请人", "申请时间", "完成申请时间","到达时间", "市场部", "出货公司",
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

                //处理状态 ,"序号", "申请流水号", "申请人", "申请时间", "完成申请时间", "市场部", "出货公司"
                //"客户", "生产事业部", "货运公司", "出货型号", "出货数量","申请原因"
                cells.Add(++rowIndex, colIndex, d.audit_result);
                cells.Add(rowIndex, ++colIndex, rowIndex - 1);
                cells.Add(rowIndex, ++colIndex, d.sys_no);
                cells.Add(rowIndex, ++colIndex, d.applier_name);
                cells.Add(rowIndex, ++colIndex, ((DateTime)d.apply_time).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, d.finish_date == null ? "" : ((DateTime)d.finish_date).ToString("yyyy-MM-dd HH:mm"));
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

        public JsonResult GetAssistantEmps(string search="", int offset = 0, int limit = 10)
        {            
            var result = (from v in db.vw_assistantEmps
                          where v.emp_name.Contains(search) || v.emp_no1.Contains(search)
                          orderby v.dept_name
                          select new AssistantEmpModel()
                          {
                              depName = v.dept_name,
                              cardNumber = v.emp_no1,
                              empName = v.emp_name,
                              empType = v.emp_type
                          });
            int total = result.Count();
            result = result.Skip(offset).Take(limit);

            return Json(new { total = total, rows = result.ToList() },JsonRequestBehavior.AllowGet);
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
        
        [SessionTimeOutFilter]
        public ActionResult ExportAuditTimeExceedExcel()
        {
            return View();
        }

        public void BeginExportAuditTimeExceedExcel(DateTime fromDate, DateTime toDate)
        {
            string dateSpan = fromDate.ToString("yyyy-MM-dd") + " _ " + toDate.ToString("yyyy-MM-dd");

            toDate = toDate.AddDays(1);
            var epSv = new EPSv();
            var ev = epSv.GetEvaluationTimeExceedRecord(fromDate, toDate); //服务评价超时记录
            var gr = epSv.GetGradeTimeExceedRecord(fromDate, toDate); //评分超时记录

            //服务评价超时汇总
            var evGroup = (from e in ev
                           group e by new { e.depName, e.name } into eg
                           select new
                           {
                               eg.Key.depName,
                               eg.Key.name,
                               sum = eg.Sum(g => g.exceedHours),
                               cou = eg.Count()
                           }).ToList();

            //评分超时汇总
            var grGroup = (from g in gr
                           group g by new { g.depName, g.name } into gg
                           select new
                           {
                               gg.Key.depName,
                               gg.Key.name,
                               sum = gg.Sum(g => g.exceedHours),
                               cou = gg.Count()
                           }).ToList();


            //設置excel文件名和sheet名
            XlsDocument xls = new XlsDocument();
            xls.FileName = "设备故障处理超时表(" + dateSpan + ")";

            //汇总标题样式
            XF titleXF = xls.NewXF();
            titleXF.HorizontalAlignment = HorizontalAlignments.Centered;
            titleXF.Font.Height = 16 * 20;
            titleXF.Font.FontName = "宋体";
            titleXF.Font.Bold = true;

            //加粗样式
            XF boldXF = xls.NewXF();
            boldXF.HorizontalAlignment = HorizontalAlignments.Centered;
            boldXF.Font.Height = 12 * 20;
            boldXF.Font.FontName = "宋体";
            boldXF.Font.Bold = true;            

            Worksheet sheet1 = xls.Workbook.Worksheets.Add("超时汇总");
            Cells cells = sheet1.Cells;

            var colWidth = new ushort[] { 24, 12, 16, 12 };            

            //设置列宽
            ColumnInfo col;            
            for (ushort i = 0; i < colWidth.Length; i++) {
                col = new ColumnInfo(xls, sheet1);
                col.ColumnIndexStart = i;
                col.ColumnIndexEnd = i;
                col.Width = (ushort)(colWidth[i] * 256);
                sheet1.AddColumnInfo(col);
                
                col = new ColumnInfo(xls, sheet1);
                col.ColumnIndexStart = (ushort)(i + 5);
                col.ColumnIndexEnd = (ushort)(i + 5);
                col.Width = (ushort)(colWidth[i] * 256);
                sheet1.AddColumnInfo(col);
            }
            
            sheet1.AddMergeArea(new MergeArea(1, 1, 1, 4));
            sheet1.AddMergeArea(new MergeArea(1, 1, 6, 9));                        

            cells.Add(1, 1, "难度评分超时汇总", titleXF);
            cells.Add(1, 6, "服务评价超时汇总", titleXF);

            
            cells.Add(2, 1, "支部名称");
            cells.Add(2, 2, "设备经理");
            cells.Add(2, 3, "累计超时(小时)");
            cells.Add(2, 4, "超时次数");

            cells.Add(2, 6, "生产车间");
            cells.Add(2, 7, "生产主管");
            cells.Add(2, 8, "累计超时(小时)");
            cells.Add(2, 9, "超时次数");

            int rowIndex = 3;
            int colIndex = 1;
            foreach (var g in grGroup.OrderBy(r => r.depName)) {
                colIndex = 1;
                cells.Add(rowIndex, colIndex, g.depName);
                cells.Add(rowIndex, ++colIndex, g.name);
                cells.Add(rowIndex, ++colIndex, g.sum);
                cells.Add(rowIndex, ++colIndex, g.cou);
                rowIndex++;
            }
            cells.Add(rowIndex, 1, "合计:");
            cells.Add(rowIndex, 3, grGroup.Sum(r => r.sum));
            cells.Add(rowIndex, 4, grGroup.Sum(r => r.cou));

            rowIndex = 3;
            colIndex = 6;
            foreach (var g in evGroup.OrderBy(r => r.depName)) {
                colIndex = 6;
                cells.Add(rowIndex, colIndex, g.depName);
                cells.Add(rowIndex, ++colIndex, g.name);
                cells.Add(rowIndex, ++colIndex, g.sum);
                cells.Add(rowIndex, ++colIndex, g.cou);
                rowIndex++;
            }
            cells.Add(rowIndex, 6, "合计:");
            cells.Add(rowIndex, 8, evGroup.Sum(r => r.sum));
            cells.Add(rowIndex, 9, evGroup.Sum(r => r.cou));

            //难度评分明细
            var colName = new string[] { "序号", "支部名称", "设备经理", "流水号", "处理开始时间", "处理结束时间", "超时小时数" };
            colWidth = new ushort[] { 8, 24, 16, 16, 16, 16, 16 };
            Worksheet sheet2 = xls.Workbook.Worksheets.Add("难度评分超时明细");
            cells = sheet2.Cells;
            rowIndex = 1;
            colIndex = 1;
            
            for (ushort i = 0; i < colWidth.Length; i++) {
                col = new ColumnInfo(xls, sheet2);
                col.ColumnIndexStart = i;
                col.ColumnIndexEnd = i;
                col.Width = (ushort)(colWidth[i] * 256);
                sheet2.AddColumnInfo(col);
            }

            //设置标题
            foreach (var name in colName) {
                cells.Add(rowIndex, colIndex++, name, boldXF);
            }

            //明细信息            
            foreach (var g in gr) {
                rowIndex++;
                colIndex = 1;
                cells.Add(rowIndex, colIndex++, rowIndex - 1);
                cells.Add(rowIndex, colIndex++, g.depName);
                cells.Add(rowIndex, colIndex++, g.name);
                cells.Add(rowIndex, colIndex++, g.sysNo);
                cells.Add(rowIndex, colIndex++, ((DateTime)g.bTime).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, colIndex++, g.eTime == null ? "---" : ((DateTime)g.eTime).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, colIndex++, g.exceedHours);
            }

            //服务评价明细
            colName = new string[] { "序号", "生产车间", "生产主管", "流水号", "处理开始时间", "处理结束时间", "超时小时数" };
            Worksheet sheet3 = xls.Workbook.Worksheets.Add("服务评价超时明细");
            cells = sheet3.Cells;
            rowIndex = 1;
            colIndex = 1;

            for (ushort i = 0; i < colWidth.Length; i++) {
                col = new ColumnInfo(xls, sheet3);
                col.ColumnIndexStart = i;
                col.ColumnIndexEnd = i;
                col.Width = (ushort)(colWidth[i] * 256);
                sheet3.AddColumnInfo(col);
            }

            //设置标题
            foreach (var name in colName) {
                cells.Add(rowIndex, colIndex++, name, boldXF);
            }

            //明细信息            
            foreach (var g in ev) {
                rowIndex++;
                colIndex = 1;
                cells.Add(rowIndex, colIndex++, rowIndex - 1);
                cells.Add(rowIndex, colIndex++, g.depName);
                cells.Add(rowIndex, colIndex++, g.name);
                cells.Add(rowIndex, colIndex++, g.sysNo);
                cells.Add(rowIndex, colIndex++, ((DateTime)g.bTime).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, colIndex++, g.eTime == null ? "---" : ((DateTime)g.eTime).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, colIndex++, g.exceedHours);
            }

            xls.Send();            

        }


        #endregion

        #region 紧急出货

        [SessionTimeOutFilter]
        public ActionResult PrintET(string sysNo)
        {
            var list = db.vw_ETReport.Where(v => v.sys_no == sysNo).ToList();
            if (list.Count() < 1) {
                ViewBag.tip = "单据不存在";
                return View("Error");
            }
            ViewData["list"] = list;

            WriteEventLog("紧急出货申请", "打印申请单：" + sysNo);
            return View();
        }

        [SessionTimeOutFilter]
        public ActionResult ETReport()
        {
            return View();
        }

        public void BeginExportETReport(DateTime fromDate, DateTime toDate)
        {
            toDate = toDate.AddDays(1);
            var result = db.vw_ETExcel.Where(u => u.apply_time > fromDate && u.apply_time <= toDate).ToList();
            
            string[] colName = new string[] { "序号", "申请流水号", "申请人", "联系电话", "申请时间", "完成申请时间", "市场部", "出货公司",
                                              "客户", "生产事业部", "出货时间", "运输方式", "总毛重(KG)","件数", "包装箱尺寸", "卡板尺寸",
                                              "送货地址", "出货要求", "申请原因", "责任备注", "货运公司", "正常运费", "申请运费", "运费差额",
                                              "订单单号", "产品名称", "规格型号", "出货数量" };
            ushort[] colWidth = new ushort[colName.Length];

            for (var i = 0; i < colWidth.Length; i++) {
                colWidth[i] = 16;
            }

            //設置excel文件名和sheet名
            XlsDocument xls = new XlsDocument();
            xls.FileName = "紧急出货运输申请列表_" + DateTime.Now.ToString("MMddHHmmss");
            Worksheet sheet = xls.Workbook.Worksheets.Add("紧急出货详情");

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

                //"序号", "申请流水号", "申请人", "联系电话", "申请时间", "完成申请时间", "市场部", "出货公司",
                //"客户", "生产事业部", "出货时间", "运输方式", "总毛重(KG)","件数", "包装箱尺寸", "卡板尺寸",
                //"送货地址", "出货要求", "申请原因", "责任备注", "货运公司", "正常运费", "申请运费", "运费差额",
                //"订单单号", "产品名称", "规格型号", "出货数量"
                cells.Add(++rowIndex, colIndex, rowIndex - 1);
                cells.Add(rowIndex, ++colIndex, d.sys_no);
                cells.Add(rowIndex, ++colIndex, d.applier_name);
                cells.Add(rowIndex, ++colIndex, d.applier_phone);
                cells.Add(rowIndex, ++colIndex, ((DateTime)d.apply_time).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, ((DateTime)d.finish_date).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, d.market_name);
                cells.Add(rowIndex, ++colIndex, d.company);

                cells.Add(rowIndex, ++colIndex, d.customer_name);
                cells.Add(rowIndex, ++colIndex, d.bus_dep);
                cells.Add(rowIndex, ++colIndex, ((DateTime)d.out_time).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, d.transfer_style);
                cells.Add(rowIndex, ++colIndex, d.gross_weight);
                cells.Add(rowIndex, ++colIndex, d.pack_num);
                cells.Add(rowIndex, ++colIndex, d.box_size);
                cells.Add(rowIndex, ++colIndex, d.cardboard_size);

                cells.Add(rowIndex, ++colIndex, d.addr);
                cells.Add(rowIndex, ++colIndex, d.demand);
                cells.Add(rowIndex, ++colIndex, d.reason);
                cells.Add(rowIndex, ++colIndex, d.responsibility);
                cells.Add(rowIndex, ++colIndex, d.dilivery_company);
                cells.Add(rowIndex, ++colIndex, d.normal_fee);
                cells.Add(rowIndex, ++colIndex, d.apply_fee);
                cells.Add(rowIndex, ++colIndex, d.different_fee);

                cells.Add(rowIndex, ++colIndex, d.order_number);
                cells.Add(rowIndex, ++colIndex, d.item_name);
                cells.Add(rowIndex, ++colIndex, d.item_modual);
                cells.Add(rowIndex, ++colIndex, d.qty);
            }

            xls.Send();
        }

        #endregion

        #region 辅料申请流程

        [SessionTimeOutFilter]
        public ActionResult APReport()
        {
            return View();
        }

        public JsonResult CheckAPReport(DateTime fromDate, DateTime toDate)
        {
            toDate = toDate.AddDays(1);
            var result = db.vw_APExcel
                .Where(u => u.apply_time > fromDate && u.apply_time <= toDate)
                .Select(u => new
                {
                    u.sys_no,
                    u.bus_name,
                    u.dep_name,
                    u.applier_name,
                    u.apply_time,
                    u.po_number,
                    u.audit_result,
                }).Distinct().OrderBy(u => u.apply_time).ToList();
            return Json(result);
        }

        public void BeginExportAPReport(DateTime fromDate, DateTime toDate)
        {
            toDate = toDate.AddDays(1);
            var result = db.vw_APExcel.Where(u => u.apply_time > fromDate && u.apply_time <= toDate).ToList();

            string[] colName = new string[] { "处理结果","申请流水号", "申请人", "公司", "事业部", "申购部门", "申请时间", "PR单号","物料代码",
                                               "物料名称", "规格型号", "实际数量", "单位","品牌","最晚到货期","使用频率","订购周期",
                                               "订购用途", "历史单价", "税率","供应商" };                                              
            ushort[] colWidth = new ushort[colName.Length];

            for (var i = 0; i < colWidth.Length; i++) {
                colWidth[i] = 16;
            }

            //設置excel文件名和sheet名
            XlsDocument xls = new XlsDocument();
            xls.FileName = "辅料订购申请列表_" + DateTime.Now.ToString("MMddHHmmss");
            Worksheet sheet = xls.Workbook.Worksheets.Add("辅料订购详情");

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
            var sv = new APSv();

            //设置标题
            foreach (var name in colName) {
                cells.Add(rowIndex, colIndex++, name, boldXF);
            }

            foreach (var d in result) {
                colIndex = 1;

                //"申请流水号", "申请人", "公司", "事业部", "申购部门", "申请时间", "PR单号","物料代码",
                //"物料名称", "规格型号", "实际数量", "单位","品牌","最晚到货期","使用频率","订购周期",
                //"订购用途", "历史单价", "税率","供应商"
                cells.Add(++rowIndex, colIndex, d.audit_result);
                cells.Add(rowIndex, ++colIndex, d.sys_no);
                cells.Add(rowIndex, ++colIndex, d.applier_name);
                cells.Add(rowIndex, ++colIndex, d.account);
                cells.Add(rowIndex, ++colIndex, d.bus_name);
                cells.Add(rowIndex, ++colIndex, d.dep_name);
                cells.Add(rowIndex, ++colIndex, ((DateTime)d.apply_time).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, d.po_number);
                cells.Add(rowIndex, ++colIndex, d.item_no);

                cells.Add(rowIndex, ++colIndex, d.item_name);
                cells.Add(rowIndex, ++colIndex, d.item_modual);
                cells.Add(rowIndex, ++colIndex, d.real_qty);
                cells.Add(rowIndex, ++colIndex, d.unit_name);
                cells.Add(rowIndex, ++colIndex, d.brand);
                cells.Add(rowIndex, ++colIndex, ((DateTime)d.latest_arrive_date).ToString("yyyy-MM-dd"));
                cells.Add(rowIndex, ++colIndex, d.using_speed);
                cells.Add(rowIndex, ++colIndex, d.order_period);

                cells.Add(rowIndex, ++colIndex, d.usage);

                var p = sv.GetItemPriceHistory(d.item_no);
                if (p != null) {
                    cells.Add(rowIndex, ++colIndex, p.price);
                    cells.Add(rowIndex, ++colIndex, p.tax_rate);
                    cells.Add(rowIndex, ++colIndex, p.supplier_name);
                }
            }

            xls.Send();
        }

        #endregion

        #region 离职流程

        [SessionTimeOutFilter]
        public ActionResult JQReport()
        {
            JQSearchParam sm;
            var cookie = Request.Cookies["jqReport"];
            if (cookie == null) {
                sm = new JQSearchParam();
                sm.fromDate = DateTime.Now.AddDays(-7).ToString("yyyy-MM-dd");
                sm.toDate = DateTime.Now.ToString("yyyy-MM-dd");
                sm.depName = "";
            }
            else {
                sm = JsonConvert.DeserializeObject<JQSearchParam>(MyUtils.DecodeToUTF8(cookie.Values.Get("sm")));
            }

            var aut = db.ei_flowAuthority.Where(f => f.bill_type == "JQ" && f.relate_type == "查询报表" && f.relate_value == userInfo.cardNo).FirstOrDefault();
            if (aut == null) {
                ViewBag.tip = "没有报表查询权限";
                return View("Error");
            }

            ViewData["canCheckDep"] = string.IsNullOrEmpty(aut.cond1) ? "所有" : aut.cond1;
            ViewData["sm"] = sm;
            return View();
        }

        public IQueryable<vw_JQExcel> SearchJQData(DateTime? fromDate, DateTime? toDate,DateTime? qFromDate,DateTime? qToDate, string depName, string empName, string sysNum)
        {
            depName = depName.Trim();

            //保存到cookie
            JQSearchParam p = new JQSearchParam();
            p.fromDate = fromDate==null?"":((DateTime)fromDate).ToString("yyyy-MM-dd");
            p.toDate = toDate==null?"":((DateTime)toDate).ToString("yyyy-MM-dd");
            p.qFromDate = qFromDate == null ? "" : ((DateTime)qFromDate).ToString("yyyy-MM-dd");
            p.qToDate = qToDate == null ? "" : ((DateTime)qToDate).ToString("yyyy-MM-dd");
            p.depName = depName;
            p.empName = empName;
            p.sysNum = sysNum;
            var cookie = new HttpCookie("jqReport");
            cookie.Values.Add("sm", MyUtils.EncodeToUTF8(JsonConvert.SerializeObject(p)));
            cookie.Expires = DateTime.Now.AddDays(30);
            Response.AppendCookie(cookie);

            IQueryable<vw_JQExcel> result = db.vw_JQExcel.Where(v => v.id > 0);
            if (fromDate != null) result = result.Where(r => r.apply_time >= fromDate);
            if (toDate != null) {
                toDate = ((DateTime)toDate).AddDays(1);
                result = result.Where(r => r.apply_time <= toDate);
            }            
            if (!string.IsNullOrWhiteSpace(depName)) result = result.Where(r => r.dep_name.Contains(depName));
            if (!string.IsNullOrWhiteSpace(empName)) result = result.Where(r => r.name.Contains(empName));
            if (!string.IsNullOrWhiteSpace(sysNum)) result = result.Where(r => r.sys_no.Contains(sysNum));
            if (qFromDate != null) result = result.Where(r => r.leave_date >= qFromDate);
            if (qToDate != null) {
                qToDate = ((DateTime)qToDate).AddDays(1);
                result = result.Where(r => r.leave_date <= qToDate);
            }

            //加上部门查询权限
            IQueryable<vw_JQExcel> datas = null;
            var depNames = db.ei_flowAuthority.Where(f => f.bill_type == "JQ" && f.relate_type == "查询报表" && f.relate_value == userInfo.cardNo).Select(f => f.cond1).FirstOrDefault();
            if (!string.IsNullOrEmpty(depNames)) {
                var names = depNames.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var name in names) {
                    if (datas == null) {
                        datas = result.Where(r => r.dep_name.Contains(name));
                    }
                    else {
                        datas = datas.Union(result.Where(r => r.dep_name.Contains(name)));
                    }
                }
            }
            else {
                datas = result;
            }

            return datas;
        }

        public JsonResult ToggleJQCheck1(string sysNo)
        {
            string check1 = "";
            try {
                var jq = db.ei_jqApply.Where(j => j.sys_no == sysNo).FirstOrDefault();
                if (jq != null) {
                    check1 = (!(jq.check1 ?? false)).ToString();
                    jq.check1 = !(jq.check1 ?? false);
                    db.SaveChanges();
                }
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel() { suc = false, msg = ex.Message });
            }
            WriteEventLog("离职报表", "切换标志1：" + sysNo + ">" + check1);
            return Json(new SimpleResultModel() { suc = true });
        }

        public JsonResult CheckJQReport(DateTime? fromDate, DateTime? toDate, DateTime? qFromDate, DateTime? qToDate, string depName = "", string empName = "", string sysNum = "")
        {
            var result = SearchJQData(fromDate, toDate, qFromDate, qToDate, depName, empName, sysNum)
                .Select(u => new
                {
                    u.sys_no,
                    u.dep_name,
                    u.name,
                    u.account,
                    u.applier_name,
                    u.apply_time,
                    u.quit_type,
                    u.audit_result,
                    u.salary_type,
                    u.check1,
                    u.leave_date
                }).OrderBy(u => u.apply_time).ToList();

            return Json(result);
        }

        public void BeginExportJQReport(DateTime? fromDate, DateTime? toDate, DateTime? qFromDate, DateTime? qToDate, string depName = "", string empName = "", string sysNum = "")
        {            
            var result = SearchJQData(fromDate, toDate, qFromDate, qToDate, depName, empName, sysNum).OrderBy(s => s.apply_time).ToList();

            string[] colName = new string[] { "处理结果","申请流水号", "申请人", "申请时间","完成时间","离职类型", "离职人", "离职人账号", "离职人厂牌", "人事部门","工资类别",
                                              "旷工开始日期", "旷工结束日期", "旷工天数", "离职原因","离职建议","工资发放方式","离职时间","组长","主管","部长","综合表现" };
            ushort[] colWidth = new ushort[colName.Length];

            for (var i = 0; i < colWidth.Length; i++) {
                colWidth[i] = 16;
            }

            //設置excel文件名和sheet名
            XlsDocument xls = new XlsDocument();
            xls.FileName = "员工离职申请列表_" + DateTime.Now.ToString("MMddHHmmss");
            Worksheet sheet = xls.Workbook.Worksheets.Add("离职申请详情");

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

                //"处理结果","申请流水号", "申请人", "申请时间",“完成时间”，"离职类型", "离职人", "离职人账号", "离职人厂牌", "人事部门","工资类别",
                //"旷工开始日期", "旷工结束日期", "旷工天数", "离职原因","离职建议","工资发放方式","离职时间","组长","主管","部长","综合表现"
                cells.Add(++rowIndex, colIndex, d.audit_result);
                cells.Add(rowIndex, ++colIndex, d.sys_no);
                cells.Add(rowIndex, ++colIndex, d.applier_name);
                cells.Add(rowIndex, ++colIndex, ((DateTime)d.apply_time).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, d.finish_date == null ? "" : ((DateTime)d.finish_date).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, d.quit_type);
                cells.Add(rowIndex, ++colIndex, d.name);
                cells.Add(rowIndex, ++colIndex, d.account);
                cells.Add(rowIndex, ++colIndex, d.card_number);
                cells.Add(rowIndex, ++colIndex, d.dep_name);
                cells.Add(rowIndex, ++colIndex, d.salary_type);

                cells.Add(rowIndex, ++colIndex, d.absent_from == null ? "" : ((DateTime)d.absent_from).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, d.absent_to == null ? "" : ((DateTime)d.absent_to).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, d.absent_days);
                cells.Add(rowIndex, ++colIndex, d.quit_reason);
                cells.Add(rowIndex, ++colIndex, d.quit_suggestion);
                cells.Add(rowIndex, ++colIndex, d.salary_clear_way);
                cells.Add(rowIndex, ++colIndex, d.leave_date == null ? "" : ((DateTime)d.leave_date).ToString("yyyy-MM-dd"));
                cells.Add(rowIndex, ++colIndex, d.group_leader_name);
                cells.Add(rowIndex, ++colIndex, d.charger_name??d.dep_charger_name);
                cells.Add(rowIndex, ++colIndex, d.produce_minister_name??d.highest_charger_name);
                cells.Add(rowIndex, ++colIndex, string.Format("工作评价:{0},{1};是否再录用：{2},{3}",d.work_evaluation,d.work_comment,d.wanna_employ,d.employ_comment));
            }

            xls.Send();
        }

        #endregion

        #region 调动流程

        [SessionTimeOutFilter]
        public ActionResult SJReport()
        {
            SJSearchParam sm;
            var cookie = Request.Cookies["sjReport"];
            if (cookie == null) {
                sm = new SJSearchParam();
                sm.fromDate = DateTime.Now.AddDays(-7).ToString("yyyy-MM-dd");
                sm.toDate = DateTime.Now.ToString("yyyy-MM-dd");
                sm.inDepName = "";
                sm.outDepName = "";
                sm.empName = "";
                sm.sysNum = "";
            }
            else {
                sm = JsonConvert.DeserializeObject<SJSearchParam>(MyUtils.DecodeToUTF8(cookie.Values.Get("sm")));
            }

            ViewData["sm"] = sm;
            return View();
        }

        public IQueryable<vw_SJExcel> SearchSJData(DateTime fromDate, DateTime toDate, string inDepName = "", string outDepName = "", string empName = "", string sysNum = "")
        {
            inDepName = inDepName.Trim();
            outDepName = outDepName.Trim();
            empName = empName.Trim();
            sysNum = sysNum.Trim();

            //保存到cookie
            SJSearchParam p = new SJSearchParam();
            p.fromDate = fromDate.ToString("yyyy-MM-dd");
            p.toDate = toDate.ToString("yyyy-MM-dd");
            p.inDepName = inDepName;
            p.outDepName = outDepName;
            p.empName = empName;
            p.sysNum = sysNum;
            var cookie = new HttpCookie("sjReport");
            cookie.Values.Add("sm", MyUtils.EncodeToUTF8(JsonConvert.SerializeObject(p)));
            cookie.Expires = DateTime.Now.AddDays(30);
            Response.AppendCookie(cookie);

            toDate = toDate.AddDays(1);
            var result = db.vw_SJExcel.Where(u => u.apply_time > fromDate && u.apply_time <= toDate);
            if (!string.IsNullOrWhiteSpace(inDepName)) result = result.Where(r => r.in_dep_name.StartsWith(inDepName));
            if (!string.IsNullOrWhiteSpace(outDepName)) result = result.Where(r => r.out_dep_name.StartsWith(outDepName));
            if (!string.IsNullOrWhiteSpace(empName)) result = result.Where(r => r.name.Contains(empName));
            if (!string.IsNullOrWhiteSpace(sysNum)) result = result.Where(r => r.sys_no.Contains(sysNum));

            return result;
        }

        public JsonResult CheckSJReport(DateTime fromDate, DateTime toDate, string inDepName = "",string outDepName="",string empName="",string sysNum="")
        {
            var result = SearchSJData(fromDate, toDate, inDepName, outDepName, empName, sysNum)
                .Select(u => new
                {
                    u.sys_no,
                    u.out_dep_name,
                    u.in_dep_name,
                    u.name,
                    u.salary_type,
                    u.account,
                    u.applier_name,
                    u.apply_time,
                    u.audit_result
                }).OrderBy(u => u.apply_time).ToList();
            return Json(result);
        }

        public void BeginExportSJReport(DateTime fromDate, DateTime toDate, string inDepName = "", string outDepName = "", string empName = "", string sysNum = "")
        {
            var result = SearchSJData(fromDate, toDate, inDepName, outDepName, empName, sysNum).OrderBy(u => u.apply_time).ToList();

            string[] colName = new string[] { "处理结果","申请流水号", "申请人", "申请时间","调动类型", "姓名", "账号", "厂牌","工资类别", "调出部门名称",
                                              "调出部门岗位", "调出时间", "调入部门名称", "调入部门岗位","到岗时间","调动原因/说明" };
            ushort[] colWidth = new ushort[colName.Length];

            for (var i = 0; i < colWidth.Length; i++) {
                colWidth[i] = 16;
            }

            //設置excel文件名和sheet名
            XlsDocument xls = new XlsDocument();
            xls.FileName = "员工调动申请列表_" + DateTime.Now.ToString("MMddHHmmss");
            Worksheet sheet = xls.Workbook.Worksheets.Add("调动申请详情");

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

                //"处理结果","申请流水号", "申请人", "申请时间","调动类型", "姓名", "账号", "厂牌","工资类别", "调出部门名称",
                //"调出部门岗位", "调出时间", "调入部门名称", "调入部门岗位","到岗时间","调动原因/说明"
                cells.Add(++rowIndex, colIndex, d.audit_result);
                cells.Add(rowIndex, ++colIndex, d.sys_no);
                cells.Add(rowIndex, ++colIndex, d.applier_name);
                cells.Add(rowIndex, ++colIndex, ((DateTime)d.apply_time).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, d.switch_type);
                cells.Add(rowIndex, ++colIndex, d.name);
                cells.Add(rowIndex, ++colIndex, d.account);
                cells.Add(rowIndex, ++colIndex, d.card_number);
                cells.Add(rowIndex, ++colIndex, d.salary_type);
                cells.Add(rowIndex, ++colIndex, d.out_dep_name);

                cells.Add(rowIndex, ++colIndex, d.out_dep_position);
                cells.Add(rowIndex, ++colIndex, d.out_time == null ? "" : ((DateTime)d.out_time).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, d.in_dep_name);
                cells.Add(rowIndex, ++colIndex, d.in_dep_position);
                cells.Add(rowIndex, ++colIndex, d.in_time == null ? "" : ((DateTime)d.in_time).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, d.comment);
            }

            xls.Send();
        }

        #endregion

        #region 部门收件/寄件流程

        public ActionResult SPReport()
        {
            return View();
        }        

        public void BeginExportSPReport(DateTime fromDate, DateTime toDate)
        {
            toDate = toDate.AddDays(1);
            var result = (from v in db.vw_spExcel
                          join entry in db.ei_spApplyEntry on v.id equals entry.sp_id into eTemp
                          from e in eTemp.DefaultIfEmpty()
                          where v.apply_time >= fromDate
                          && v.apply_time <= toDate
                          select new
                          {
                              v,
                              e
                          }).ToList();

            string[] colName = new string[] { "处理结果", "公司", "部门", "申请人","快递公司","运费","快递单号","快递方式", "收寄类型", "收寄范围", "流水号", "申请时间"
                                            , "联系电话","放行条编号", "收寄内容","产品名称","规格型号","配送数量","件数","总净重","卡板数"
                                            , "卡板尺寸","装箱尺寸","配送时效","寄件地址","收件地址","收件人","收件人电话","申请原因","是否退补货"
            };
            ushort[] colWidth = new ushort[colName.Length];

            for (var i = 0; i < colWidth.Length; i++) {
                colWidth[i] = 16;
            }

            //設置excel文件名和sheet名
            XlsDocument xls = new XlsDocument();
            xls.FileName = "部门收寄件申请列表_" + DateTime.Now.ToString("MMddHHmmss");
            Worksheet sheet = xls.Workbook.Worksheets.Add("收件寄件详情");

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

                //"处理结果", "公司", "部门", "申请人","快递公司","运费","快递单号","快递方式", "收寄类型", "收寄范围", "流水号", "申请时间"
                //, "联系电话","放行条编号", "收寄内容","产品名称","规格型号","配送数量","件数","总净重","卡板数"
                //, "卡板尺寸","装箱尺寸","配送时效","寄件地址","收件地址","收件人","收件人电话","申请原因"
                cells.Add(++rowIndex, colIndex, d.v.audit_result);
                cells.Add(rowIndex, ++colIndex, d.v.company);
                cells.Add(rowIndex, ++colIndex, d.v.bus_name);
                cells.Add(rowIndex, ++colIndex, d.v.applier_name);
                cells.Add(rowIndex, ++colIndex, d.v.ex_company);
                cells.Add(rowIndex, ++colIndex, d.v.ex_price);
                cells.Add(rowIndex, ++colIndex, d.v.ex_no);
                cells.Add(rowIndex, ++colIndex, d.v.ex_type);
                cells.Add(rowIndex, ++colIndex, d.v.send_or_receive);
                cells.Add(rowIndex, ++colIndex, d.v.scope);
                cells.Add(rowIndex, ++colIndex, d.v.sys_no);
                cells.Add(rowIndex, ++colIndex, ((DateTime)d.v.apply_time).ToString("yyyy-MM-dd HH:mm"));

                cells.Add(rowIndex, ++colIndex, d.v.applier_phone);
                cells.Add(rowIndex, ++colIndex, d.v.send_no);
                cells.Add(rowIndex, ++colIndex, d.v.content_type);
                cells.Add(rowIndex, ++colIndex, d.e == null ? "" : d.e.name);
                cells.Add(rowIndex, ++colIndex, d.e == null ? "" : d.e.modual);
                cells.Add(rowIndex, ++colIndex, d.e == null ? "" : d.e.qty.ToString());
                cells.Add(rowIndex, ++colIndex, d.v.package_num);
                cells.Add(rowIndex, ++colIndex, d.v.total_weight);
                cells.Add(rowIndex, ++colIndex, d.v.cardboard_num);

                cells.Add(rowIndex, ++colIndex, d.v.cardboard_size);
                cells.Add(rowIndex, ++colIndex, d.v.box_size);
                cells.Add(rowIndex, ++colIndex, d.v.aging);
                cells.Add(rowIndex, ++colIndex, d.v.from_addr);
                cells.Add(rowIndex, ++colIndex, d.v.to_addr);
                cells.Add(rowIndex, ++colIndex, d.v.receiver_name);
                cells.Add(rowIndex, ++colIndex, d.v.receiver_phone);
                cells.Add(rowIndex, ++colIndex, d.v.apply_reason);
                cells.Add(rowIndex, ++colIndex, d.v.isReturnBack);
            }

            xls.Send();
        }

        public ActionResult PrintSP(string sysNo)
        {
            var list = db.vw_SPReport.Where(s => s.sys_no == sysNo).ToList();
            if (list.Count()==0) {
                ViewBag.tip = "单据不存在或行政部还未审批";
                return View("Error");
            }
            ViewData["list"] = list;
            return View();
        }

        #endregion

        #region 物流车辆放行

        //门卫查看界面
        public ActionResult TIViewForGuard()
        {
            return View();
        }

        //导出报表界面
        public ActionResult TIReport()
        {
            return View();
        }

        public IQueryable<vw_TIExcel> GetTIData(DateTime fromDate, DateTime toDate)
        {
            return db.vw_TIExcel.Where(t => t.in_day >= fromDate && t.in_day <= toDate);
        }

        public JsonResult SearchTIDataForGuard(DateTime fromDate, DateTime toDate)
        {
            return Json(GetTIData(fromDate, toDate).Where(t => t.audit_result == "通过").ToList());
        }

        public JsonResult SearchTIDataForReport(DateTime fromDate, DateTime toDate)
        {
            return Json(GetTIData(fromDate, toDate).ToList());
        }

        public JsonResult ChangeTIStatus(int entryId, string status)
        {
            try {
                new TISv().ChangeStatus(entryId,status);
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel() { suc = false, msg = ex.Message });
            }
            return Json(new SimpleResultModel() { suc = true });
        }

        public void BeginExportTIExcel(DateTime fromDate, DateTime toDate)
        {
            var result = GetTIData(fromDate, toDate).ToList();
            string[] colName = new string[] { "处理结果","申请流水号", "申请人", "申请时间","进厂日期", "进厂时间", "司机姓名", "身份证号","车辆类型", "车牌号码",
                                              "备注","状态" };
            ushort[] colWidth = new ushort[colName.Length];

            for (var i = 0; i < colWidth.Length; i++) {
                colWidth[i] = 16;
            }

            //設置excel文件名和sheet名
            XlsDocument xls = new XlsDocument();
            xls.FileName = "物流车辆放行申请列表_" + DateTime.Now.ToString("MMddHHmmss");
            Worksheet sheet = xls.Workbook.Worksheets.Add("车辆放行申请详情");

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

                //"处理结果","申请流水号", "申请人", "申请时间","进厂日期", "进厂时间", "司机姓名", "身份证号","车辆类型", "车牌号码",
                //"备注"
                cells.Add(++rowIndex, colIndex, d.audit_result);
                cells.Add(rowIndex, ++colIndex, d.sys_no);
                cells.Add(rowIndex, ++colIndex, d.applier_name);
                cells.Add(rowIndex, ++colIndex, ((DateTime)d.apply_time).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, ((DateTime)d.in_day).ToString("yyyy-MM-dd"));
                cells.Add(rowIndex, ++colIndex, d.in_timespan);
                cells.Add(rowIndex, ++colIndex, d.driver_name);
                cells.Add(rowIndex, ++colIndex, d.driver_no);
                cells.Add(rowIndex, ++colIndex, d.car_type);
                cells.Add(rowIndex, ++colIndex, d.car_no);

                cells.Add(rowIndex, ++colIndex, d.comment);
                cells.Add(rowIndex, ++colIndex, d.t_status);
            }

            xls.Send();
        }

        #endregion

    }
}
