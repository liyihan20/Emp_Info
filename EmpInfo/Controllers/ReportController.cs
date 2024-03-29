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
using EmpInfo.Services;


namespace EmpInfo.Controllers
{
    public class ReportController : BaseController
    {

        #region 公共方法

        /// <summary>
        /// 获取审核步骤名称和审核人姓名
        /// </summary>
        /// <param name="sysNo"></param>
        /// <returns></returns>
        public List<StepNameAndAuditor> GetAuditorList(string sysNo)
        {
            var auditorList = (from a in db.flow_apply
                               join ad in db.flow_applyEntry on a.id equals ad.apply_id
                               join u in db.ei_users on ad.final_auditor equals u.card_number
                               where a.sys_no == sysNo && ad.final_auditor != null
                               select new StepNameAndAuditor()
                               {
                                   stepName = ad.step_name,
                                   auditorName = u.name,
                                   auditorNo = u.card_number,
                                   auditTime = ad.audit_time
                               }).ToList();
            return auditorList;
        }

        #endregion

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
            cookie.Expires = DateTime.Now.AddDays(60);
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
                                               10, 10, 12, 16, 24, 20, 40, 16,24,10,10,36 };

            string[] colName = new string[] { "状态", "申请人", "条形码", "申请时间", "审批完成时间", "部门", "职位级别", "是否直管", "开始时间", "结束时间", 
                                              "天数", "小时数", "类型", "去向", "事由", "工作代理", "知会人", "流水号","当前审核人","标志1","标志2","查看链接" };

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
                cells.Add(rowIndex, ++colIndex, d.go_where);
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
            var list = flow.GetAuditList(userInfo.cardNo, "", "", "", "", "", "", new ArrayOfInt() { 0 }, new ArrayOfInt() { 0 }, new ArrayOfString() { "AL" }, 600).ToList();
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
                                               10, 10, 12, 16, 24, 16, 16, 16, 16, 16};

            string[] colName = new string[] { "申请人", "申请时间", "部门", "职位级别", "开始时间", "结束时间", 
                                              "天数", "小时数", "类型", "去向","事由", "流水号","面谈通知","预约时间","发送时间","发送人" };

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
                cells.Add(rowIndex, ++colIndex, d.al.go_where);
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

            ushort[] colWidth = new ushort[] { 20, 16, 40, 12, 18, 16, 24 };

            string[] colName = new string[] { "申请人", "申请时间", "请假时间", "类型", "流水号", "审批结果","意见" };

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
                cells.Add(rowIndex, ++colIndex, d.opinion);
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

            if (al.work_days < days) {
                return Json(new SimpleResultModel() { suc = false, msg = "2021-06-26起：请假天数不能改多，只能改少。如要延假，请联系申请人再提交请假条" });
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

        [SessionTimeOutFilter]
        public ActionResult SRBillReport()
        {
            return View();
        }

        public void BeginExportSRBills(DateTime fromDate, DateTime toDate, int exportType = 0)
        {
            toDate = toDate.AddDays(1);
            var result = db.GetSRBills(fromDate, toDate).ToList().OrderBy(r => r.FOutTimeSpan).ThenBy(r => r.FCompany).ToList();

            if (exportType == 0) {
                result = result.Where(r => r.FStatus == "平台未确认").ToList();
            }

            string[] colName = new string[] { "公司", "出货期间", "到货日期", "当前状态", "出库单号", "申请单号","申请人", "部门", "客户名称", "出货公司",
                                              "出货地址", "出货联系人", "出货联系电话", "订单号", "规格型号", "出货数量" };

            //設置excel文件名和sheet名
            XlsDocument xls = new XlsDocument();
            xls.FileName = "出货申请列表_" + DateTime.Now.ToString("MMddHHmmss");
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
            for (ushort i = 0; i < colName.Length; i++) {
                col = new ColumnInfo(xls, sheet);
                col.ColumnIndexStart = i;
                col.ColumnIndexEnd = i;
                col.Width = (ushort)(18 * 256);
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

                //"公司", "出货期间", "到货日期", "当前状态", "出库单号", "申请单号","申请人", "客户名称", "出货公司",
                //"出货地址", "出货联系人", "出货联系电话", "订单号", "出货数量" 
                cells.Add(++rowIndex, colIndex, d.FCompany);
                cells.Add(rowIndex, ++colIndex, d.FOutTimeSpan);
                cells.Add(rowIndex, ++colIndex, d.FArriveDate);
                cells.Add(rowIndex, ++colIndex, d.FStatus);
                cells.Add(rowIndex, ++colIndex, d.FStockNo);
                cells.Add(rowIndex, ++colIndex, d.FBillNo);
                cells.Add(rowIndex, ++colIndex, d.FApplier);
                cells.Add(rowIndex, ++colIndex, d.FdeptName);
                cells.Add(rowIndex, ++colIndex, d.FSupplierName);
                cells.Add(rowIndex, ++colIndex, d.FShipToName);

                cells.Add(rowIndex, ++colIndex, d.FShipToAddress);
                cells.Add(rowIndex, ++colIndex, d.FShipToAttn);
                cells.Add(rowIndex, ++colIndex, d.FShipToTel);
                cells.Add(rowIndex, ++colIndex, d.FOrderBillNo);
                cells.Add(rowIndex, ++colIndex, d.FModel);
                cells.Add(rowIndex, ++colIndex, d.FQty);
            }

            xls.Send();
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
            var datas = GetSVDatas(depId, vFromDate, vToDate,dFromDate,dToDate, auditStatus, empName, salaryNo).ToList();
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

        #region 漏刷卡流程

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
            var datas = GetCRDatas(depId, fromDate, toDate, auditStatus, empName, salaryNo).ToList();
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
            var myData = GetCRDatas(depId, fromDate, toDate, auditStatus, empName, salaryNo).ToList().OrderBy(m => m.apply_time).ToList();
            var dep = db.ei_department.Single(d => d.id == depId);

            ushort[] colWidth = new ushort[] { 10, 20, 12, 16,16, 60, 12, 18,
                                               14, 24, 20, 24,10,10 };

            string[] colName = new string[] { "状态", "申请人", "条形码", "申请时间","审批完成时间", "部门",  "漏刷日期", "漏刷时间", 
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
                cells.Add(rowIndex, ++colIndex, (d.finish_date == null ? "" : ((DateTime)d.finish_date).ToString("yyyy-MM-dd HH:mm")));
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
                                               16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16,
                                               16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16 };

            string[] colName = new string[] { "状态", "申请流水号", "报修日期", "报修时间", "报修人", "联系电话", "设备支部", "事业部", "车间名称", "岗位位置", 
                                              "设备名称", "设备型号", "固定资产类别", "固定资产编号","设备供应商","生产主管","设备经理","影响停产程度",
                                              "故障现象", "接单时间","接单人", "延迟处理原因","处理登记时间","处理完成时间","接单用时(分)","维修用时(分)","维修总用时(分)", "维修人员","故障原因类别","故障原因",
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
                //"故障现象", "接单时间","接单人","处理完成时间","接单用时(分)","维修用时(分)","维修总用时(分)","维修人员","故障原因类别","故障原因",
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
                                
                var reportTime = DateTime.Parse(((DateTime)d.report_time).ToString("yyyy-MM-dd HH:mm"));
                if (d.accept_time == null) {
                    cells.Add(rowIndex, ++colIndex, "");
                }
                else {
                    var acceptTime = DateTime.Parse(((DateTime)d.accept_time).ToString("yyyy-MM-dd HH:mm"));
                    cells.Add(rowIndex, ++colIndex, (acceptTime - reportTime).TotalMinutes);
                }
                if (d.confirm_time == null || d.confirm_register_time == null) {
                    cells.Add(rowIndex, ++colIndex, "");
                    cells.Add(rowIndex, ++colIndex, "");
                }
                else {
                    var acceptTime = DateTime.Parse(((DateTime)d.accept_time).ToString("yyyy-MM-dd HH:mm"));
                    var confirmTime = DateTime.Parse(((DateTime)d.confirm_time).ToString("yyyy-MM-dd HH:mm"));
                    cells.Add(rowIndex, ++colIndex, (confirmTime - acceptTime).TotalMinutes);
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
            
            string[] colName = new string[] { "序号", "审核状态", "申请流水号", "申请人", "联系电话", "申请时间", "完成申请时间", "市场部", "出货公司",
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

                //"序号", "审核状态", "申请流水号", "申请人", "联系电话", "申请时间", "完成申请时间", "市场部", "出货公司",
                //"客户", "生产事业部", "出货时间", "运输方式", "总毛重(KG)","件数", "包装箱尺寸", "卡板尺寸",
                //"送货地址", "出货要求", "申请原因", "责任备注", "货运公司", "正常运费", "申请运费", "运费差额",
                //"订单单号", "产品名称", "规格型号", "出货数量"
                cells.Add(++rowIndex, colIndex, rowIndex - 1);
                cells.Add(rowIndex, ++colIndex, d.audit_result);
                cells.Add(rowIndex, ++colIndex, d.sys_no);
                cells.Add(rowIndex, ++colIndex, d.applier_name);
                cells.Add(rowIndex, ++colIndex, d.applier_phone);
                cells.Add(rowIndex, ++colIndex, ((DateTime)d.apply_time).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, d.finish_date == null ? "" : ((DateTime)d.finish_date).ToString("yyyy-MM-dd HH:mm"));
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
            p.fromDate = fromDate == null ? "" : ((DateTime)fromDate).ToString("yyyy-MM-dd");
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
                var names = depNames.Split(new char[] { ';','；' }, StringSplitOptions.RemoveEmptyEntries);
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
                                              "旷工开始日期", "旷工结束日期", "旷工天数", "离职原因","离职建议","工资发放方式","入职时间","离职时间","标识1", "组长","主管","部长","综合表现" };
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
                string inDate = ""; //入职日期
                if (d.card_number != null && d.card_number.Length > 6) {
                    var yearSpan = d.card_number.Substring(0, 2);
                    int year;
                    if (Int32.TryParse(yearSpan, out year)) {
                        if (year < 80) {
                            inDate = "20" + yearSpan + "-" + d.card_number.Substring(2, 2) + "-" + d.card_number.Substring(4, 2);
                        }
                        else {
                            inDate = "19" + yearSpan + "-" + d.card_number.Substring(2, 2) + "-" + d.card_number.Substring(4, 2);
                        }
                    }
                    
                }

                //"处理结果","申请流水号", "申请人", "申请时间",“完成时间”，"离职类型", "离职人", "离职人账号", "离职人厂牌", "人事部门","工资类别",
                //"旷工开始日期", "旷工结束日期", "旷工天数", "离职原因","离职建议","工资发放方式","离职时间","组长","主管","部长","综合表现"
                cells.Add(++rowIndex, colIndex, d.audit_result);
                cells.Add(rowIndex, ++colIndex, d.sys_no);
                cells.Add(rowIndex, ++colIndex, d.applier_name);
                cells.Add(rowIndex, ++colIndex, ((DateTime)d.apply_time).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, d.finish_date == null ? "" : ((DateTime)d.finish_date).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, d.quit_type.Equals("自离") ? "自动离职" : d.quit_type);
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
                cells.Add(rowIndex, ++colIndex, inDate);
                cells.Add(rowIndex, ++colIndex, d.leave_date == null ? "" : ((DateTime)d.leave_date).ToString("yyyy-MM-dd"));
                cells.Add(rowIndex, ++colIndex, d.check1 == true ? "Y" : "");
                cells.Add(rowIndex, ++colIndex, d.group_leader_name);
                cells.Add(rowIndex, ++colIndex, d.charger_name??d.dep_charger_name);
                cells.Add(rowIndex, ++colIndex, d.produce_minister_name??d.highest_charger_name);
                cells.Add(rowIndex, ++colIndex, string.Format("工作评价:{0},{1};是否再录用：{2},{3}",d.work_evaluation,d.work_comment,d.wanna_employ,d.employ_comment));
            }

            xls.Send();
        }

        #endregion

        #region 计件辞职新流程

        [SessionTimeOutFilter]
        public ActionResult MQReport()
        {
            JQSearchParam sm;
            var cookie = Request.Cookies["mqReport"];
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

        public IQueryable<vw_MQExcel> SearchMQData(DateTime? fromDate, DateTime? toDate, DateTime? qFromDate, DateTime? qToDate, string depName, string empName, string sysNum)
        {
            depName = depName.Trim();

            //保存到cookie
            JQSearchParam p = new JQSearchParam();
            p.fromDate = fromDate == null ? "" : ((DateTime)fromDate).ToString("yyyy-MM-dd");
            p.toDate = toDate == null ? "" : ((DateTime)toDate).ToString("yyyy-MM-dd");
            p.qFromDate = qFromDate == null ? "" : ((DateTime)qFromDate).ToString("yyyy-MM-dd");
            p.qToDate = qToDate == null ? "" : ((DateTime)qToDate).ToString("yyyy-MM-dd");
            p.depName = depName;
            p.empName = empName;
            p.sysNum = sysNum;
            var cookie = new HttpCookie("mqReport");
            cookie.Values.Add("sm", MyUtils.EncodeToUTF8(JsonConvert.SerializeObject(p)));
            cookie.Expires = DateTime.Now.AddDays(30);
            Response.AppendCookie(cookie);

            IQueryable<vw_MQExcel> result = db.vw_MQExcel.Where(v => v.id > 0);
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
            IQueryable<vw_MQExcel> datas = null;
            var depNames = db.ei_flowAuthority.Where(f => f.bill_type == "JQ" && f.relate_type == "查询报表" && f.relate_value == userInfo.cardNo).Select(f => f.cond1).FirstOrDefault();
            if (!string.IsNullOrEmpty(depNames)) {
                var names = depNames.Split(new char[] { ';', '；' }, StringSplitOptions.RemoveEmptyEntries);
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

        public JsonResult ToggleMQCheck1(string sysNo)
        {
            string check1 = "";
            try {
                var mq = db.ei_mqApply.Where(j => j.sys_no == sysNo).FirstOrDefault();
                if (mq != null) {
                    check1 = (!(mq.check1 ?? false)).ToString();
                    mq.check1 = !(mq.check1 ?? false);
                    db.SaveChanges();
                }
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel() { suc = false, msg = ex.Message });
            }
            WriteEventLog("离职报表", "切换标志1：" + sysNo + ">" + check1);
            return Json(new SimpleResultModel() { suc = true });
        }

        public JsonResult CheckMQReport(DateTime? fromDate, DateTime? toDate, DateTime? qFromDate, DateTime? qToDate, string depName = "", string empName = "", string sysNum = "")
        {
            var result = SearchMQData(fromDate, toDate, qFromDate, qToDate, depName, empName, sysNum)
                .Select(u => new
                {
                    u.sys_no,
                    u.dep_name,
                    u.name,
                    u.account,
                    u.apply_time,
                    u.audit_result,
                    u.check1,
                    u.leave_date
                }).OrderBy(u => u.apply_time).ToList();

            return Json(result);
        }

        public void BeginExportMQReport(DateTime? fromDate, DateTime? toDate, DateTime? qFromDate, DateTime? qToDate, string depName = "", string empName = "", string sysNum = "")
        {
            var result = SearchMQData(fromDate, toDate, qFromDate, qToDate, depName, empName, sysNum).OrderBy(s => s.apply_time).ToList();

            string[] colName = new string[] { "处理结果","申请流水号", "申请人", "申请时间","完成时间", "账号", "厂牌", "人事部门","离职原因","离职建议","工资发放方式",
                                        "入职时间","离职时间","标识1", "组长","组长是否已谈","组长处理方式","主管","主管是否已谈","主管处理方式","生产部长","综合表现" };
            ushort[] colWidth = new ushort[colName.Length];

            for (var i = 0; i < colWidth.Length; i++) {
                colWidth[i] = 16;
            }

            //設置excel文件名和sheet名
            XlsDocument xls = new XlsDocument();
            xls.FileName = "计件员工辞职申请列表_" + DateTime.Now.ToString("MMddHHmmss");
            Worksheet sheet = xls.Workbook.Worksheets.Add("辞职申请详情");

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
                string inDate = ""; //入职日期
                if (d.card_number != null && d.card_number.Length > 6) {
                    var yearSpan = d.card_number.Substring(0, 2);
                    int year;
                    if (Int32.TryParse(yearSpan, out year)) {
                        if (year < 80) {
                            inDate = "20" + yearSpan + "-" + d.card_number.Substring(2, 2) + "-" + d.card_number.Substring(4, 2);
                        }
                        else {
                            inDate = "19" + yearSpan + "-" + d.card_number.Substring(2, 2) + "-" + d.card_number.Substring(4, 2);
                        }
                    }

                }

                //"处理结果","申请流水号", "申请人", "申请时间","完成时间", "账号", "人事部门","离职原因","离职建议","工资发放方式",
                //"入职时间","离职时间","标识1", "组长","组长是否已面谈","组长处理方式","主管","主管是否已面谈","主管处理方式","生产部长","综合表现"
                cells.Add(++rowIndex, colIndex, d.audit_result);
                cells.Add(rowIndex, ++colIndex, d.sys_no);
                cells.Add(rowIndex, ++colIndex, d.applier_name);
                cells.Add(rowIndex, ++colIndex, ((DateTime)d.apply_time).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, d.finish_date == null ? "" : ((DateTime)d.finish_date).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, d.account);
                cells.Add(rowIndex, ++colIndex, d.applier_num);
                cells.Add(rowIndex, ++colIndex, d.dep_name);
                cells.Add(rowIndex, ++colIndex, d.quit_reason);
                cells.Add(rowIndex, ++colIndex, d.quit_suggestion);
                cells.Add(rowIndex, ++colIndex, d.salary_clear_way);

                cells.Add(rowIndex, ++colIndex, inDate);
                cells.Add(rowIndex, ++colIndex, d.leave_date == null ? "" : ((DateTime)d.leave_date).ToString("yyyy-MM-dd"));
                cells.Add(rowIndex, ++colIndex, d.check1 == true ? "Y" : "");
                cells.Add(rowIndex, ++colIndex, d.group_leader_name);
                cells.Add(rowIndex, ++colIndex, d.group_leader_talked?"是":"否");
                cells.Add(rowIndex, ++colIndex, d.group_leader_choise);
                cells.Add(rowIndex, ++colIndex, d.charger_name);
                cells.Add(rowIndex, ++colIndex, d.charger_talked?"是":"否");
                cells.Add(rowIndex, ++colIndex, d.charger_choise);
                cells.Add(rowIndex, ++colIndex, d.produce_minister_name);
                cells.Add(rowIndex, ++colIndex, string.Format("工作评价:{0},{1};是否再录用：{2},{3}", d.work_evaluation, d.work_comment, d.wanna_employ, d.employ_comment));
            }

            xls.Send();
        }
        
        #endregion

        #region 离职流程整合JM

        [SessionTimeOutFilter]
        public ActionResult JMReport()
        {
            JMSearchParam sm;
            var cookie = Request.Cookies["jmReport"];
            if (cookie == null) {
                sm = new JMSearchParam();
                sm.fromDate = DateTime.Now.AddDays(-7).ToString("yyyy-MM-dd");
                sm.toDate = DateTime.Now.ToString("yyyy-MM-dd");
                sm.depName = "";
            }
            else {
                sm = JsonConvert.DeserializeObject<JMSearchParam>(MyUtils.DecodeToUTF8(cookie.Values.Get("sm")));
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

        public IQueryable<vw_JMExcel> SearchJMData(string searchJmJson)
        {           
            //保存到cookie           
            var cookie = new HttpCookie("jmReport");
            cookie.Values.Add("sm", MyUtils.EncodeToUTF8(searchJmJson));
            cookie.Expires = DateTime.Now.AddDays(30);
            Response.AppendCookie(cookie);

            JMSearchParam p = JsonConvert.DeserializeObject<JMSearchParam>(searchJmJson);
            IQueryable<vw_JMExcel> result = db.vw_JMExcel.Where(v => v.id > 0);
            DateTime fromDate,toDate,qFromDate,qToDate;
            if (!string.IsNullOrEmpty(p.fromDate)){
                fromDate=DateTime.Parse(p.fromDate);
                result = result.Where(r => r.apply_time >= fromDate);
            }
            if (!string.IsNullOrEmpty(p.toDate)) {
                toDate = DateTime.Parse(p.toDate).AddDays(1);
                result = result.Where(r => r.apply_time <= toDate);
            }
            if (!string.IsNullOrWhiteSpace(p.depName)) result = result.Where(r => r.dep_name.Contains(p.depName));
            if (!string.IsNullOrWhiteSpace(p.empName)) result = result.Where(r => r.name.Contains(p.empName));
            if (!string.IsNullOrWhiteSpace(p.sysNum)) result = result.Where(r => r.sys_no.Contains(p.sysNum));
            if (!string.IsNullOrWhiteSpace(p.cardNumber)) result = result.Where(r => r.card_number.Contains(p.cardNumber));
            if (!string.IsNullOrWhiteSpace(p.salaryNumber)) result = result.Where(r => r.account.Contains(p.salaryNumber));
            if (!string.IsNullOrWhiteSpace(p.quitType)) result = result.Where(r => r.quit_type.Contains(p.quitType));
            if (!string.IsNullOrWhiteSpace(p.salaryType)) result = result.Where(r => r.salary_type.Contains(p.salaryType));

            if (!string.IsNullOrEmpty(p.qFromDate)) {
                qFromDate = DateTime.Parse(p.qFromDate);
                result = result.Where(r => r.leave_date >= qFromDate);
            }
            if (!string.IsNullOrEmpty(p.qToDate)) {
                qToDate = DateTime.Parse(p.qToDate).AddDays(1);
                result = result.Where(r => r.leave_date <= qToDate);
            }

            //加上部门查询权限
            IQueryable<vw_JMExcel> datas = null;
            var depNames = db.ei_flowAuthority.Where(f => f.bill_type == "JQ" && f.relate_type == "查询报表" && f.relate_value == userInfo.cardNo).Select(f => f.cond1).FirstOrDefault();
            if (!string.IsNullOrEmpty(depNames)) {
                var names = depNames.Split(new char[] { ';', '；' }, StringSplitOptions.RemoveEmptyEntries);
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

        public JsonResult ToggleJMCheck1(string sysNo)
        {
            string check1 = "";
            try {
                if (sysNo.StartsWith("JQ")) {
                    check1 = new JQSv(sysNo).ToggleCheck1();
                }
                if (sysNo.StartsWith("MQ")) {
                    check1 = new MQSv(sysNo).ToggleCheck1();
                }
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel() { suc = false, msg = ex.Message });
            }
            WriteEventLog("离职报表", "切换标志1：" + sysNo + ">" + check1);
            return Json(new SimpleResultModel() { suc = true });
        }

        public JsonResult CheckJMReport(string searchJmJson)
        {
            var result = SearchJMData(searchJmJson)
                .Select(u => new
                {
                    u.sys_no,
                    u.dep_name,
                    u.name,
                    u.account,
                    u.apply_time,
                    u.audit_result,
                    u.check1,
                    u.leave_date,
                    u.salary_type,
                    u.quit_type
                }).OrderBy(u => u.apply_time).ToList();

            return Json(result);
        }

        public void BeginExportJMReport(string searchJmJson)
        {
            var result = SearchJMData(searchJmJson).OrderBy(s => s.apply_time).ToList();

            string[] colName = new string[] { "处理结果","申请流水号", "申请人", "申请时间","完成时间","离职类型", "离职人", "离职人账号", "离职人厂牌", "人事部门","工资类别",
                                              "离职原因","离职建议","工资发放方式","入职时间","离职时间","标识1", "组长","主管","部长","综合表现" };
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
                string inDate = ""; //入职日期
                if (d.card_number != null && d.card_number.Length > 6) {
                    var yearSpan = d.card_number.Substring(0, 2);
                    int year;
                    if (Int32.TryParse(yearSpan, out year)) {
                        if (year < 80) {
                            inDate = "20" + yearSpan + "-" + d.card_number.Substring(2, 2) + "-" + d.card_number.Substring(4, 2);
                        }
                        else {
                            inDate = "19" + yearSpan + "-" + d.card_number.Substring(2, 2) + "-" + d.card_number.Substring(4, 2);
                        }
                    }

                }

                //"处理结果","申请流水号", "申请人", "申请时间",“完成时间”，"离职类型", "离职人", "离职人账号", "离职人厂牌", "人事部门","工资类别",
                //"旷工开始日期", "旷工结束日期", "旷工天数", "离职原因","离职建议","工资发放方式","离职时间","组长","主管","部长","综合表现"
                cells.Add(++rowIndex, colIndex, d.audit_result);
                cells.Add(rowIndex, ++colIndex, d.sys_no);
                cells.Add(rowIndex, ++colIndex, d.applier_name);
                cells.Add(rowIndex, ++colIndex, ((DateTime)d.apply_time).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, d.finish_date == null ? "" : ((DateTime)d.finish_date).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, d.quit_type.Equals("自离") ? "自动离职" : d.quit_type);
                cells.Add(rowIndex, ++colIndex, d.name);
                cells.Add(rowIndex, ++colIndex, d.account);
                cells.Add(rowIndex, ++colIndex, d.card_number);
                cells.Add(rowIndex, ++colIndex, d.dep_name);
                cells.Add(rowIndex, ++colIndex, d.salary_type);

                //cells.Add(rowIndex, ++colIndex, d.absent_from == null ? "" : ((DateTime)d.absent_from).ToString("yyyy-MM-dd HH:mm"));
                //cells.Add(rowIndex, ++colIndex, d.absent_to == null ? "" : ((DateTime)d.absent_to).ToString("yyyy-MM-dd HH:mm"));
                //cells.Add(rowIndex, ++colIndex, d.absent_days);
                cells.Add(rowIndex, ++colIndex, d.quit_reason);
                cells.Add(rowIndex, ++colIndex, d.quit_suggestion);
                cells.Add(rowIndex, ++colIndex, d.salary_clear_way);
                cells.Add(rowIndex, ++colIndex, inDate);
                cells.Add(rowIndex, ++colIndex, d.leave_date == null ? "" : ((DateTime)d.leave_date).ToString("yyyy-MM-dd"));
                cells.Add(rowIndex, ++colIndex, d.check1 == true ? "Y" : "");
                cells.Add(rowIndex, ++colIndex, d.group_leader_name);
                cells.Add(rowIndex, ++colIndex, d.charger_name ?? d.dep_charger_name);
                cells.Add(rowIndex, ++colIndex, d.produce_minister_name ?? d.highest_charger_name);
                cells.Add(rowIndex, ++colIndex, string.Format("工作评价:{0},{1};是否再录用：{2},{3}", d.work_evaluation, d.work_comment, d.wanna_employ, d.employ_comment));
            }

            xls.Send();
        }

        //人事面谈报表
        [SessionTimeOutFilter]
        public ActionResult MQHRTalkReport()
        {
            return View();
        }

        public JsonResult GetMQHRTalkReport(string fromDate, string toDate, string empName, string status)
        {
            DateTime fd, td;
            if (!DateTime.TryParse(fromDate, out fd)) {
                return Json(new SimpleResultModel(false, "开始日期不合法"));
            }
            if (!DateTime.TryParse(toDate, out td)) {
                return Json(new SimpleResultModel(false, "借宿日期不合法"));
            }
            td = td.AddDays(1);

            var result = from v in db.vw_MQExcel
                         join h in db.ei_mqHRTalkRecord on v.sys_no equals h.sys_no
                         where h.in_time >= fd && h.in_time <= td
                         select new
                         {
                             h.id,
                             h.in_time,
                             h.t_status,
                             h.sys_no,
                             v.name,
                             v.dep_name,
                             v.leave_date,
                             v.audit_result,
                             h.talk_result,
                             v.card_number
                         };
            if (!string.IsNullOrEmpty(empName)) {
                result = result.Where(r => r.name.Contains(empName));
            }
            if (!string.IsNullOrEmpty(status)) {
                result = result.Where(r => r.t_status == status);
            }
            return Json(new SimpleResultModel(true, "查询完成", JsonConvert.SerializeObject(result.OrderBy(r => r.id).ToList())));
        }

        public void BeginExportMQHRTalkReport(string fromDate, string toDate, string empName, string status)
        {
            DateTime fd, td;
            if (!DateTime.TryParse(fromDate, out fd)) {
                return;
            }
            if (!DateTime.TryParse(toDate, out td)) {
                return;
            }
            td = td.AddDays(1);

            var result = from v in db.vw_MQExcel
                         join h in db.ei_mqHRTalkRecord on v.sys_no equals h.sys_no
                         where h.in_time >= fd && h.in_time <= td
                         select new
                         {                             
                             h.in_time,
                             h.t_status,
                             h.sys_no,
                             v.name,
                             v.dep_name,
                             v.leave_date,
                             v.audit_result,
                             h.talk_result,
                             h.talk_time,
                             v.card_number
                         };
            if (!string.IsNullOrEmpty(empName)) {
                result = result.Where(r => r.name.Contains(empName));
            }
            if (!string.IsNullOrEmpty(status)) {
                result = result.Where(r => r.t_status == status);
            }

            string[] colName = new string[] { "申请流水号", "人事部门", "离职人","厂牌", "离职时间","处理结果", "预约时间", "面谈状态", "面谈结果","面谈处理时间" };
            ushort[] colWidth = new ushort[colName.Length];

            for (var i = 0; i < colWidth.Length; i++) {
                colWidth[i] = 16;
            }

            //設置excel文件名和sheet名
            XlsDocument xls = new XlsDocument();
            xls.FileName = "人事面谈列表_" + DateTime.Now.ToString("MMddHHmmss");
            Worksheet sheet = xls.Workbook.Worksheets.Add("面谈详情");

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

                //"申请流水号", "人事部门", "离职人", "离职时间","处理结果", "预约时间", "面谈状态", "面谈结果","面谈处理时间"
                cells.Add(++rowIndex, colIndex, d.sys_no);
                cells.Add(rowIndex, ++colIndex, d.dep_name);
                cells.Add(rowIndex, ++colIndex, d.name);
                cells.Add(rowIndex, ++colIndex, d.card_number);
                cells.Add(rowIndex, ++colIndex, d.leave_date==null?"":((DateTime)d.leave_date).ToString("yyyy-MM-dd"));
                cells.Add(rowIndex, ++colIndex, d.audit_result);
                cells.Add(rowIndex, ++colIndex, d.in_time == null ? "" : ((DateTime)d.in_time).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, d.t_status);
                cells.Add(rowIndex, ++colIndex, d.talk_result);
                cells.Add(rowIndex, ++colIndex, d.talk_time == null ? "" : ((DateTime)d.talk_time).ToString("yyyy-MM-dd HH:mm"));
            }

            xls.Send();

        }

        public JsonResult SetHRHasTalked(int id,string talkResult="")
        {
            var h = db.ei_mqHRTalkRecord.Where(r => r.id == id).FirstOrDefault();
            if (h != null) {
                h.t_status = "面谈完成";
                h.talk_time = DateTime.Now;
                h.talk_result = talkResult;
                db.SaveChanges();
            }
            return Json(new SimpleResultModel(true));
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

        public JsonResult SearchSPReport(DateTime fromDate, DateTime toDate)
        {
            toDate = toDate.AddDays(1);
            var result = (from v in db.vw_spExcel
                          where v.apply_time >= fromDate
                          && v.apply_time <= toDate
                          select v).ToList();
            return Json(result);
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
                                            , "卡板尺寸","装箱尺寸","配送时效","寄件地址","收件地址","收件人","收件人电话","申请原因","是否信利产品"
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
                ViewBag.tip = "此放行条已放行或不能打印";
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
            string[] colName = new string[] { "处理结果","申请流水号", "申请人", "申请时间","进厂日期", "进厂时间", "货运公司", "司机姓名", "身份证号","车辆类型", "车牌号码",
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
                cells.Add(rowIndex, ++colIndex, d.ex_company);
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

        #region 电脑维修

        [SessionTimeOutFilter]
        public ActionResult ITReport()
        {
            return View();
        }

        public IQueryable<vw_ITExcel> SearchITDatas(DateTime fromDate, DateTime toDate, string depName, string accepterName, string applierName,string sysNo)
        {
            toDate = toDate.AddDays(1);
            var result = from v in db.vw_ITExcel
                         where v.apply_time >= fromDate
                         && v.apply_time < toDate
                         select v;
            if (!string.IsNullOrWhiteSpace(depName)) {
                result = result.Where(r => r.dep_name.Contains(depName));
            }
            if (!string.IsNullOrWhiteSpace(accepterName)) {
                result = result.Where(r => r.accept_man_name.Contains(accepterName));
            }
            if (!string.IsNullOrWhiteSpace(applierName)) {
                result = result.Where(r => r.applier_name.Contains(applierName));
            }
            if (!string.IsNullOrWhiteSpace(sysNo)) {
                result = result.Where(r => r.sys_no.Contains(sysNo));
            }

            return result;
        }

        public JsonResult GetITDatas(DateTime fromDate, DateTime toDate, string depName, string accepterName, string applierName, string sysNo)
        {
            var result = SearchITDatas(fromDate, toDate, depName, accepterName, applierName, sysNo).Select(r => new
            {
                r.applier_name,
                r.apply_time,
                r.dep_name,
                r.accept_man_name,
                r.audit_result,
                r.repair_way,
                r.sys_no,
                r.fetch_time
            }).OrderBy(r => r.apply_time).ToList();

            return Json(result);
        }

        public void ExportITExcel(DateTime fromDate, DateTime toDate, string depName, string accepterName, string applierName, string sysNo)
        {
            var result = SearchITDatas(fromDate, toDate, depName, accepterName, applierName, sysNo).OrderBy(r => r.apply_time).ToList();
            string[] colName = new string[] { "处理进度","申请流水号", "申请人", "申请时间", "联系电话", "职位级别","部门", "设备类别", "电脑编号","计算机名", "IP地址",
                                              "申请项目", "申请备注", "主管/部门经理", "接单人","接单时间","维修途径", "标签打印时间", "处理人", "处理时间","处理项目","实际产生IT费用",
                                              "IT部备注","取回时间", "取走人", "取走人电话", "评价时间", "服务打分", "评价意见" };
            ushort[] colWidth = new ushort[colName.Length];

            for (var i = 0; i < colWidth.Length; i++) {
                colWidth[i] = 24;
            }

            //設置excel文件名和sheet名
            XlsDocument xls = new XlsDocument();
            xls.FileName = "电脑维修申请列表_" + DateTime.Now.ToString("MMddHHmmss");
            Worksheet sheet = xls.Workbook.Worksheets.Add("电脑维修申请详情");

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

                //"处理进度","申请流水号", "申请人", "申请时间", "联系电话", "职位级别","部门", "电脑编号","计算机名", "IP地址",
                //"申请项目", "申请备注", "主管/部门经理", "接单人","接单时间","维修途径", "标签打印时间", "处理人", "处理时间","处理项目","实际产生IT费用",
                //"IT部备注","取回时间", "取走人", "取走人电话", "评价时间", "服务打分", "评价意见"
                cells.Add(++rowIndex, colIndex, d.audit_result);
                cells.Add(rowIndex, ++colIndex, d.sys_no);
                cells.Add(rowIndex, ++colIndex, d.applier_name);
                cells.Add(rowIndex, ++colIndex, ((DateTime)d.apply_time).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, d.applier_phone);
                cells.Add(rowIndex, ++colIndex, d.emp_position);
                cells.Add(rowIndex, ++colIndex, d.dep_name);
                cells.Add(rowIndex, ++colIndex, d.equitment_type);
                cells.Add(rowIndex, ++colIndex, d.computer_number);
                cells.Add(rowIndex, ++colIndex, d.computer_name);
                cells.Add(rowIndex, ++colIndex, d.ip_addr);

                cells.Add(rowIndex, ++colIndex, ParseItItems(d.faulty_items));
                cells.Add(rowIndex, ++colIndex, d.applier_comment);
                cells.Add(rowIndex, ++colIndex, d.dep_charger_name);
                cells.Add(rowIndex, ++colIndex, d.accept_man_name);
                cells.Add(rowIndex, ++colIndex, d.accept_time == null ? "" : ((DateTime)d.accept_time).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, d.repair_way);
                cells.Add(rowIndex, ++colIndex, d.print_time == null ? "" : ((DateTime)d.print_time).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, d.repair_man);
                cells.Add(rowIndex, ++colIndex, d.repair_time == null ? "" : ((DateTime)d.repair_time).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, ParseItItems(d.fixed_items));
                cells.Add(rowIndex, ++colIndex, d.real_it_fee);

                cells.Add(rowIndex, ++colIndex, d.it_comment);
                cells.Add(rowIndex, ++colIndex, d.fetch_time == null ? "" : ((DateTime)d.fetch_time).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, d.fetcher_name);
                cells.Add(rowIndex, ++colIndex, d.fetcher_phone);
                cells.Add(rowIndex, ++colIndex, d.evaluation_time == null ? "" : ((DateTime)d.evaluation_time).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, d.evaluation_score);
                cells.Add(rowIndex, ++colIndex, d.evaluation_comment);
            }

            xls.Send();
        }

        private string ParseItItems(string items)
        {
            var list = JsonConvert.DeserializeObject<List<ItItem>>(items);
            string result = "";
            foreach (var i in list) {
                result += i.n + ":" + i.v + ";";
            }
            return result;
        }

        // 已维修完成的数据
        public ActionResult ITFixedRecord()
        {
            return View();
        }

        public JsonResult GetITFixedDatas(DateTime fromDate, DateTime toDate, bool hasFetched)
        {

            toDate = toDate.AddDays(1);
            var result = (from i in db.ei_itApply
                          where i.repair_time != null
                          && i.repair_way == "现场维修"
                          && i.repair_time >= fromDate && i.repair_time < toDate
                          && ((hasFetched && i.fetch_time != null) || (!hasFetched && i.fetch_time == null))
                          orderby i.repair_time
                          select new
                          {
                              i.applier_name,
                              i.repair_time,
                              i.dep_name,
                              i.accept_man_name,
                              i.sys_no,
                              i.fetch_time
                          }).ToList();
            return Json(result);
        }

        #endregion

        #region 后勤工程支出

        public void ExportDEExcel(string sysNo)
        {
            var ds = (from de in db.ei_DEApply
                      join e in db.ei_DEApplyEntry on de.id equals e.de_id
                      where de.sys_no == sysNo
                      select new
                      {
                          de.sys_no,
                          de.applier_name,
                          de.bill_date,
                          e.catalog,
                          e.clear_date,
                          e.comment,
                          e.name,
                          e.qty,
                          e.subject,
                          e.summary,
                          e.supplier_name,
                          e.tax_rate,
                          e.total,
                          e.total_with_tax,
                          e.unit_name,
                          e.unit_price
                      }).ToList();
            if (ds.Count() == 0) return;

            string[] colName = new string[] { "单号","类别", "项目", "名称", "摘要", "单位","数量", "供应商", "单价","金额", "税率", "价税合计", "备注" };
            ushort[] colWidth = new ushort[colName.Length];

            for (var i = 0; i < colWidth.Length; i++) {
                colWidth[i] = 12;
            }

            //設置excel文件名和sheet名
            XlsDocument xls = new XlsDocument();
            xls.FileName = "后勤工程支出_" + sysNo;
            Worksheet sheet = xls.Workbook.Worksheets.Add("申请详情");

            //设置各种样式

            //标题样式
            XF boldXF = xls.NewXF();
            boldXF.HorizontalAlignment = HorizontalAlignments.Centered;
            boldXF.Font.Height = 20 * 20;
            boldXF.Font.FontName = "宋体";
            //boldXF.Font.Bold = true;

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

            cells.Merge(1, 1, 1, 13);
            cells.Add(rowIndex, colIndex, "后勤部项目申请表",boldXF);

            cells.Add(++rowIndex, 2, "日期：");
            cells.Add(rowIndex, 3, ((DateTime)ds.First().bill_date).ToString("yyyy-MM-dd"));

            rowIndex++;
            //设置列名
            foreach (var name in colName) {
                cells.Add(rowIndex, colIndex++, name);
            }

            foreach (var d in ds) {
                colIndex = 1;

                //"单号","类别", "项目", "名称", "摘要", "单位","数量", "供应商", "单价","金额", "税率", "价税合计", "备注"
                cells.Add(++rowIndex, colIndex, d.sys_no);
                cells.Add(rowIndex, ++colIndex, d.catalog);
                cells.Add(rowIndex, ++colIndex, d.subject);
                cells.Add(rowIndex, ++colIndex, d.name);
                cells.Add(rowIndex, ++colIndex, d.summary);
                cells.Add(rowIndex, ++colIndex, d.unit_name);
                cells.Add(rowIndex, ++colIndex, d.qty);
                cells.Add(rowIndex, ++colIndex, d.supplier_name);
                cells.Add(rowIndex, ++colIndex, d.unit_price);
                cells.Add(rowIndex, ++colIndex, d.total);
                cells.Add(rowIndex, ++colIndex, d.tax_rate);
                cells.Add(rowIndex, ++colIndex, d.total_with_tax);
                cells.Add(rowIndex, ++colIndex, d.comment);
            }

            rowIndex += 3;

            //签名
            cells.Add(rowIndex, 2, "申请人：");
            cells.Add(rowIndex, 6, "审核：");
            cells.Add(rowIndex, 10, "审批人：");
            cells.Add(++rowIndex, 2, "日期：");
            cells.Add(rowIndex, 6, "日期：");
            cells.Add(rowIndex, 10, "日期：");

            xls.Send();

        }

        #endregion

        #region 开源节流

        [SessionTimeOutFilter]
        public ActionResult KSReport()
        {
            return View();
        }

        public IQueryable<vw_KSExcel> SearchKSDatas(DateTime fromDate, DateTime toDate, string applierName,string executorName)
        {
            toDate = toDate.AddDays(1);
            var result = from v in db.vw_KSExcel
                         where v.apply_time >= fromDate
                         && v.apply_time < toDate
                         select v;
            if (!string.IsNullOrWhiteSpace(applierName)) {
                result = result.Where(r => r.applier_name.Contains(applierName));
            }
            if (!string.IsNullOrWhiteSpace(executorName)) {
                result = result.Where(r => r.executor_name.Contains(executorName));
            }
            return result;
        }

        public JsonResult GetKSDatas(DateTime fromDate, DateTime toDate, string applierName, string executorName)
        {
            var result = SearchKSDatas(fromDate, toDate, applierName,executorName).Select(r => new
            {
                r.applier_name,
                r.apply_time,
                r.dep_name,
                r.executor_name,
                r.audit_result,
                r.level_name,
                r.sys_no,
            }).OrderBy(r => r.apply_time).ToList();

            return Json(result);
        }

        public void ExportKSExcel(DateTime fromDate, DateTime toDate, string applierName, string executorName)
        {
            var result = SearchKSDatas(fromDate, toDate, applierName, executorName).OrderBy(r => r.apply_time).ToList();
            string[] colName = new string[] { "处理进度","申请流水号", "申请人", "申请时间", "联系电话", "部门", "现状", "建议","收益", "采纳奖励",
                                              "评级", "评级奖励", "执行人", "营运部意见","团队组员","成果说明", "营运部备注" };
            ushort[] colWidth = new ushort[colName.Length];

            for (var i = 0; i < colWidth.Length; i++) {
                colWidth[i] = 24;
            }

            //設置excel文件名和sheet名
            XlsDocument xls = new XlsDocument();
            xls.FileName = "开源节流申请列表_" + DateTime.Now.ToString("MMddHHmmss");
            Worksheet sheet = xls.Workbook.Worksheets.Add("申请详情");

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

                //"处理进度","申请流水号", "申请人", "申请时间", "联系电话", "部门", "现状", "建议","收益", "采纳奖励",
                //"评级", "评级奖励", "执行人", "营运部意见","团队组员","成果说明", "营运部备注"
                cells.Add(++rowIndex, colIndex, d.audit_result);
                cells.Add(rowIndex, ++colIndex, d.sys_no);
                cells.Add(rowIndex, ++colIndex, d.applier_name);
                cells.Add(rowIndex, ++colIndex, ((DateTime)d.apply_time).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, d.applier_phone);
                cells.Add(rowIndex, ++colIndex, d.dep_name);
                cells.Add(rowIndex, ++colIndex, d.situation);
                cells.Add(rowIndex, ++colIndex, d.suggestion);
                cells.Add(rowIndex, ++colIndex, d.benefit);
                cells.Add(rowIndex, ++colIndex, d.applier_reward);
                
                cells.Add(rowIndex, ++colIndex, d.level_name);
                cells.Add(rowIndex, ++colIndex, d.level_reward);
                cells.Add(rowIndex, ++colIndex, d.executor_name);
                cells.Add(rowIndex, ++colIndex, d.operation_dep_opinion);
                cells.Add(rowIndex, ++colIndex, d.group_members);
                cells.Add(rowIndex, ++colIndex, d.result_description);
                cells.Add(rowIndex, ++colIndex, d.operation_dep_summary);
            }

            xls.Send();
        }

        #endregion

        #region 换货申请

        //放行条
        public ActionResult PrintHH(string sysNo)
        {
            var hs = from h in db.ei_hhApply
                     join e in db.ei_hhApplyEntry on h.id equals e.hh_id
                     where h.sys_no == sysNo
                     select new { h, e };
            if (hs.Count() < 1) {
                ViewBag.tip = "单据不存在，不能打印放行条";
                return View("Error");
            }
            if (hs.Where(i => i.e.send_qty == null).Count() > 0) {
                ViewBag.tip = "存在未录入的实发数量，不能打印放行条";
                return View("Error");
            }
            if (hs.First().h.out_time != null) {
                ViewBag.tip = "此放行条已放行，不能再打印";
                return View("Error");
            }
            var auditorList = GetAuditorList(sysNo);
            var result = new HHCheckApplyModel()
            {
                head = hs.First().h,
                entrys = hs.Select(i => i.e).ToList(),
                auditorList = auditorList
            };

            new HHSv(sysNo).UpdatePrintStatus();

            ViewData["m"] = result;
            return View();
        }

        //打印申请单给事业部留底
        public ActionResult PrintHHForBus(string sysNo)
        {
            var hs = from h in db.ei_hhApply
                     join e in db.ei_hhApplyEntry on h.id equals e.hh_id
                     where h.sys_no == sysNo
                     select new { h, e };
            if (hs.Count() < 1) {
                ViewBag.tip = "单据不存在，不能打印";
                return View("Error");
            }
            var auditorList = GetAuditorList(sysNo);

            if (auditorList.Where(a => a.stepName.Contains("总经理")).Count() < 1) {
                ViewBag.tip = "事业部总经理还未审批，不能打印";
                return View("Error");
            }

            var result = new HHCheckApplyModel()
            {
                head = hs.First().h,
                entrys = hs.Select(i => i.e).ToList(),
                auditorList = auditorList
            };

            new HHSv(sysNo).UpdatePrintStatus();

            ViewData["m"] = result;
            return View();
        }

        public ActionResult HHReport()
        {
            return View();
        }

        public JsonResult SearchHHReport(string obj)
        {
            HHSearchReportModel m = JsonConvert.DeserializeObject<HHSearchReportModel>(obj);

            var result = from h in db.ei_hhApply
                         join e in db.ei_hhApplyEntry on h.id equals e.hh_id
                         join a in db.flow_apply on h.sys_no equals a.sys_no
                         where h.return_date >= m.beginDate && h.return_date <= m.endDate
                         select new
                         {
                             h.sys_no,
                             h.applier_name,
                             h.return_date,
                             h.company,
                             h.customer_name,
                             h.return_dep,
                             //e.order_no,
                             e.moduel,
                             e.return_qty,
                             audit_result = a.success == true ? "已结案" : (a.success == false ? "已拒绝" : "审批中")
                         };
            if (!string.IsNullOrWhiteSpace(m.sysNo)) {
                result = result.Where(r => r.sys_no.Contains(m.sysNo));
            }
            if (!"所有".Equals(m.auditResult)) {
                result = result.Where(r => r.audit_result == m.auditResult);
            }
            if (!string.IsNullOrWhiteSpace(m.applierName)) {
                result = result.Where(r => r.applier_name == m.applierName);
            }
            if (!string.IsNullOrWhiteSpace(m.returnDep)) {
                result = result.Where(r => r.return_dep.Contains(m.returnDep));
            }
            if (!string.IsNullOrWhiteSpace(m.customerName)) {
                result = result.Where(r => r.customer_name.Contains(m.customerName));
            }
            //if (!string.IsNullOrWhiteSpace(m.orderNo)) {
            //    result = result.Where(r => r.order_no.Contains(m.orderNo));
            //}
            if (!string.IsNullOrWhiteSpace(m.moduel)) {
                result = result.Where(r => r.moduel.Contains(m.moduel));
            }

            return Json(result.ToList());
        }

        public void ExportHHExcel(string obj)
        {
            HHSearchReportModel m = JsonConvert.DeserializeObject<HHSearchReportModel>(obj);

            var result = from h in db.ei_hhApply
                         join e in db.ei_hhApplyEntry on h.id equals e.hh_id
                         join a in db.flow_apply on h.sys_no equals a.sys_no
                         where h.return_date >= m.beginDate && h.return_date <= m.endDate
                         select new
                         {
                             h,
                             e,
                             audit_result = a.success == true ? "已结案" : (a.success == false ? "已拒绝" : "审批中")
                         };
            if (!string.IsNullOrWhiteSpace(m.sysNo)) {
                result = result.Where(r => r.h.sys_no.Contains(m.sysNo));
            }
            if (!"所有".Equals(m.auditResult)) {
                result = result.Where(r => r.audit_result == m.auditResult);
            }
            if (!string.IsNullOrWhiteSpace(m.applierName)) {
                result = result.Where(r => r.h.applier_name == m.applierName);
            }
            if (!string.IsNullOrWhiteSpace(m.returnDep)) {
                result = result.Where(r => r.h.return_dep.Contains(m.returnDep));
            }
            if (!string.IsNullOrWhiteSpace(m.customerName)) {
                result = result.Where(r => r.h.customer_name.Contains(m.customerName));
            }
            if (!string.IsNullOrWhiteSpace(m.orderNo)) {
                result = result.Where(r => r.e.order_no.Contains(m.orderNo));
            }
            if (!string.IsNullOrWhiteSpace(m.moduel)) {
                result = result.Where(r => r.e.moduel.Contains(m.moduel));
            }

            string[] colName = new string[] { "处理结果","申请流水号", "申请人", "申请时间", "出货公司", "事业部", "市场部", "客户名称","品质经理", "收货地址",
                                              "换货原因", "订单号", "规格型号", "退货数量","实退数量","是否已上线", "补货数量","实发数量", "发货人" };
            ushort[] colWidth = new ushort[colName.Length];

            for (var i = 0; i < colWidth.Length; i++) {
                colWidth[i] = 16;
            }

            //設置excel文件名和sheet名
            XlsDocument xls = new XlsDocument();
            xls.FileName = "换货申请列表_" + DateTime.Now.ToString("MMddHHmmss");
            Worksheet sheet = xls.Workbook.Worksheets.Add("申请详情");

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

            foreach (var d in result.ToList()) {
                colIndex = 1;

                //"处理结果","申请流水号", "申请人", "申请时间", "出货公司", "事业部", "市场部", "客户名称","品质经理", "收货地址",
                //"换货原因", "订单号", "规格型号", "退货数量","实退数量","是否已上线", "补货数量","实发数量", "发货人"
                cells.Add(++rowIndex, colIndex, d.audit_result);
                cells.Add(rowIndex, ++colIndex, d.h.sys_no);
                cells.Add(rowIndex, ++colIndex, d.h.applier_name);
                cells.Add(rowIndex, ++colIndex, d.h.apply_time.ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, d.h.company);
                cells.Add(rowIndex, ++colIndex, d.h.return_dep);
                cells.Add(rowIndex, ++colIndex, d.h.agency_name);
                cells.Add(rowIndex, ++colIndex, d.h.customer_name);
                cells.Add(rowIndex, ++colIndex, d.h.quality_manager_name);
                cells.Add(rowIndex, ++colIndex, d.h.return_addr);

                cells.Add(rowIndex, ++colIndex, d.h.return_reason);
                cells.Add(rowIndex, ++colIndex, d.e.order_no);
                cells.Add(rowIndex, ++colIndex, d.e.moduel);
                cells.Add(rowIndex, ++colIndex, d.e.return_qty);
                cells.Add(rowIndex, ++colIndex, d.e.real_return_qty);
                cells.Add(rowIndex, ++colIndex, d.e.is_on_line);
                cells.Add(rowIndex, ++colIndex, d.e.fill_qty);
                cells.Add(rowIndex, ++colIndex, d.e.send_qty);
                cells.Add(rowIndex, ++colIndex, d.e.sender_name);
            }

            xls.Send();
        }

        #endregion

        #region 项目单

        [SessionTimeOutFilter]
        public ActionResult PrintXA(string sysNo)
        {
            var bill = db.ei_xaApply.Where(x => x.sys_no == sysNo).FirstOrDefault();
            if (bill == null) {
                ViewBag.tip = "单据不存在";
                return View("Error");
            }
            if (!bill.can_print) {
                ViewBag.tip = "不能打印";
                return View("Error");
            }

            var bidder = db.ei_xaApplySupplier.Where(x => x.sys_no == sysNo && x.is_bidder == true).FirstOrDefault();
            var auditorList=GetAuditorList(sysNo);
            var result = (from r in db.flow_auditorRelation
                          where r.bill_type == "XA"
                          && (
                          //(r.relate_name == "部门总经理" && r.relate_text == bill.company + "_" + bill.dept_name) ||
                          (r.relate_name == "项目大类" && r.relate_text == bill.classification)
                          ||  (r.relate_name == "节省与监督")
                          )
                          select r).ToList();

            //将会签中的步骤设置一下
            foreach (var l in auditorList.Where(a => a.stepName == "管理部会签")) {
                l.stepName = result.Where(r => r.relate_value == l.auditorNo).Select(r => r.relate_name).FirstOrDefault() ?? "";
            }

            ViewData["bill"] = bill;
            ViewData["bidder"] = bidder;
            ViewData["auditorList"] = auditorList;
            ViewData["username"] = userInfo.name;

            return View();
        }

        public ActionResult XAReport()
        {
            return View();
        }

        public JsonResult SearchXAReport(string obj)
        {
            XASearchReportModel m = JsonConvert.DeserializeObject<XASearchReportModel>(obj);

            var result = from x in db.ei_xaApply
                         join a in db.flow_apply on x.sys_no equals a.sys_no
                         join aet in db.flow_applyEntry on new { id = a.id, pass = (bool?)null } equals new { id = (int)aet.apply_id, pass = aet.pass } into aettemp
                         from ae in aettemp.DefaultIfEmpty()
                         //where x.apply_time >= m.fromDate
                         //&& x.apply_time < m.toDate
                         select new
                         {
                             id = x.id,
                             sysNo = x.sys_no,
                             auditStatus = a.user_abort == true ? "撤销" : (a.success == true ? "已通过" : (a.success == false ? "已拒绝" : ae.step_name)),
                             applierName = x.applier_name,
                             applyTime = x.apply_time,
                             projectName = x.project_name,
                             classification = x.classification,
                             billNo = x.bill_no,
                             company = x.company,
                             deptName = x.dept_name,
                             conformDate = x.confirm_date
                         };

            //var buyerRelation = db.flow_auditorRelation.Where(f => f.bill_type == "XA" && f.relate_name == "采购部审批" && f.relate_value == userInfo.cardNo).ToList();
            //if (buyerRelation.Count() > 0) {
            //    var companyList = buyerRelation.Select(b => b.relate_text).ToList();
            //    result = result.Where(r => companyList.Contains(r.company));
            //}

            if (m.fromDate != null && m.toDate != null) {
                m.toDate = ((DateTime)m.toDate).AddDays(1);
                result = result.Where(r => r.applyTime >= m.fromDate && r.applyTime < m.toDate);
            }

            if (m.confirmFromDate != null && m.confirmToDate != null) {
                m.confirmToDate = ((DateTime)m.confirmToDate).AddDays(1);
                result = result.Where(r => r.conformDate >= m.confirmFromDate && r.conformDate < m.confirmToDate);
            }

            var canCheckBus = db.ei_flowAuthority.Where(f => f.bill_type == "XA" && f.relate_type == "查询报表" && f.relate_value == userInfo.cardNo).FirstOrDefault();
            if (canCheckBus != null && !string.IsNullOrEmpty(canCheckBus.cond1)) {
                var buses = canCheckBus.cond1.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                result = result.Where(r => buses.Contains(r.deptName));
            }

            if (!string.IsNullOrEmpty(m.classification)) {
                result = result.Where(r => r.classification == m.classification);
            }
            if (!string.IsNullOrEmpty(m.applierName)) {
                result = result.Where(r => r.applierName.Contains(m.applierName));
            }
            if (!string.IsNullOrEmpty(m.sysNo)) {
                result = result.Where(r => r.sysNo.Contains(m.sysNo));
            }
            if (!string.IsNullOrEmpty(m.projectName)) {
                result = result.Where(r => r.projectName.Contains(m.projectName));
            }
            if (!string.IsNullOrEmpty(m.deptName)) {
                result = result.Where(r => r.deptName.Contains(m.deptName));
            }
            if (!string.IsNullOrEmpty(m.currentNode)) {
                result = result.Where(r => r.auditStatus.Contains(m.currentNode));
            }


            return Json(result.Distinct().OrderBy(r => r.id).ToList());
        }

        public void ExportXAExcel(string obj)
        {
            XASearchReportModel m = JsonConvert.DeserializeObject<XASearchReportModel>(obj);

            var result = from x in db.ei_xaApply
                         join a in db.flow_apply on x.sys_no equals a.sys_no
                         join st in db.ei_xaApplySupplier on new { sys_no = x.sys_no, is_bidder = true } equals new { sys_no = st.sys_no, is_bidder = st.is_bidder } into stemp
                         from s in stemp.DefaultIfEmpty()
                         join aet in db.flow_applyEntry on new { id = a.id, pass = (bool?)null } equals new { id = (int)aet.apply_id, pass = aet.pass } into aettemp
                         from ae in aettemp.DefaultIfEmpty()
                         orderby x.id
                         //where x.apply_time >= m.fromDate
                         //&& x.apply_time < m.toDate
                         select new
                         {
                             x,
                             s,
                             auditResult = a.user_abort == true ? "撤销" : (a.success == true ? "已通过" : (a.success == false ? "已拒绝" : ae.step_name)),
                         };

            //var buyerRelation = db.flow_auditorRelation.Where(f => f.bill_type == "XA" && f.relate_name == "采购部审批" && f.relate_value == userInfo.cardNo).ToList();
            //if (buyerRelation.Count() > 0) {
            //    var companyList = buyerRelation.Select(b => b.relate_text).ToList();
            //    result = result.Where(r => companyList.Contains(r.x.company));
            //}

            if (m.fromDate != null && m.toDate != null) {
                m.toDate = ((DateTime)m.toDate).AddDays(1);
                result = result.Where(r => r.x.apply_time >= m.fromDate && r.x.apply_time < m.toDate);
            }

            if (m.confirmFromDate != null && m.confirmToDate != null) {
                m.confirmToDate = ((DateTime)m.confirmToDate).AddDays(1);
                result = result.Where(r => r.x.confirm_date >= m.confirmFromDate && r.x.confirm_date < m.confirmToDate);
            }

            var canCheckBus = db.ei_flowAuthority.Where(f => f.bill_type == "XA" && f.relate_type == "查询报表" && f.relate_value == userInfo.cardNo).FirstOrDefault();
            if (canCheckBus != null && !string.IsNullOrEmpty(canCheckBus.cond1)) {
                var buses = canCheckBus.cond1.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                result = result.Where(r => buses.Contains(r.x.dept_name));
            }

            if (!string.IsNullOrEmpty(m.classification)) {
                result = result.Where(r => r.x.classification == m.classification);
            }
            if (!string.IsNullOrEmpty(m.applierName)) {
                result = result.Where(r => r.x.applier_name.Contains(m.applierName));
            }
            if (!string.IsNullOrEmpty(m.sysNo)) {
                result = result.Where(r => r.x.sys_no.Contains(m.sysNo));
            }
            if (!string.IsNullOrEmpty(m.projectName)) {
                result = result.Where(r => r.x.project_name.Contains(m.projectName));
            }
            if (!string.IsNullOrEmpty(m.deptName)) {
                result = result.Where(r => r.x.dept_name.Contains(m.deptName));
            }
            if (!string.IsNullOrEmpty(m.currentNode)) {
                result = result.Where(r => r.auditResult.Contains(m.currentNode));
            }

            string[] colName = new string[] { "处理结果","申请流水号", "申请人", "联系电话", "申请时间","审核部确认时间","公司", "申请部门", "项目编号", "项目名称","地点", "项目大类",
                                              "项目类别", "申请原因", "具体要求","是否多部门分摊","分摊详情", "项目收益","人员节省","产能收益","其它收益","是否为PO单","PO单号", "中标供应商","中标价格","税率","不含税价格" };
            ushort[] colWidth = new ushort[colName.Length];

            for (var i = 0; i < colWidth.Length; i++) {
                colWidth[i] = 16;
            }

            //設置excel文件名和sheet名
            XlsDocument xls = new XlsDocument();
            xls.FileName = "项目单申请列表_" + DateTime.Now.ToString("MMddHHmmss");
            Worksheet sheet = xls.Workbook.Worksheets.Add("申请详情");

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

            foreach (var r in result.Distinct().ToList()) {
                colIndex = 1;
                var d = r.x;
                //"处理结果","申请流水号", "申请人", "联系电话", "申请时间", "申请部门", "项目编号", "项目名称","地点", "项目大类",
                //"项目类别", "申请原因", "具体要求", "项目收益","人员节省","产能收益","其它收益", "中标供应商","中标价格"
                cells.Add(++rowIndex, colIndex, r.auditResult);
                cells.Add(rowIndex, ++colIndex, d.sys_no);
                cells.Add(rowIndex, ++colIndex, d.applier_name);
                cells.Add(rowIndex, ++colIndex, d.applier_phone);
                cells.Add(rowIndex, ++colIndex, d.apply_time.ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, d.confirm_date == null ? "" : ((DateTime)d.confirm_date).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, d.company);
                cells.Add(rowIndex, ++colIndex, d.dept_name);
                cells.Add(rowIndex, ++colIndex, d.bill_no);
                cells.Add(rowIndex, ++colIndex, d.project_name);
                cells.Add(rowIndex, ++colIndex, d.addr);
                cells.Add(rowIndex, ++colIndex, d.classification);

                cells.Add(rowIndex, ++colIndex, d.project_type);
                cells.Add(rowIndex, ++colIndex, d.reason);
                cells.Add(rowIndex, ++colIndex, d.demands.Replace("\br", ";"));
                cells.Add(rowIndex, ++colIndex, d.is_share_fee?"是":"否");
                cells.Add(rowIndex, ++colIndex, d.share_fee_detail);
                cells.Add(rowIndex, ++colIndex, d.has_profit ? "有项目收益" : "无项目收益（维修维护类）");
                cells.Add(rowIndex, ++colIndex, d.save_people);
                cells.Add(rowIndex, ++colIndex, d.productivity_profit);
                cells.Add(rowIndex, ++colIndex, d.other_profit);
                cells.Add(rowIndex, ++colIndex, d.is_po == null ? "" : (d.is_po == true ? "是" : "否"));
                cells.Add(rowIndex, ++colIndex, d.po_no);
                if (r.s != null) {
                    cells.Add(rowIndex, ++colIndex, r.s.supplier_name);
                    cells.Add(rowIndex, ++colIndex, r.s.price);
                    if (r.s.tax_rate != null) {
                        cells.Add(rowIndex, ++colIndex, r.s.tax_rate);
                        cells.Add(rowIndex, ++colIndex, Math.Round((decimal)r.s.price / (1 + (int)r.s.tax_rate), 2));
                    }
                }
            }

            xls.Send();

        }

        public ActionResult XASummary()
        {
            return View();
        }

        public JsonResult SearchXASummary(string year, string month,bool? isPO,string groupName="生产部")
        {
            DateTime fromDate, toDate;
            if ("整年".Equals(month)) {
                fromDate = DateTime.Parse(year + "-01-01");
                toDate = fromDate.AddYears(1);
            }
            else {
                fromDate = DateTime.Parse(year + "-" + month + "-01");
                toDate = fromDate.AddMonths(1);
            }

            var result = (from x in db.ei_xaApply
                          join s in db.ei_xaApplySupplier on new { x.sys_no, is_bidder = true } equals new { s.sys_no, s.is_bidder }
                          where x.confirm_date != null && x.confirm_date >= fromDate && x.confirm_date < toDate
                          && (isPO == null || x.is_po == isPO)
                          select new
                          {
                              x,
                              s
                          }).ToList();

            List<stringDecimalModel> list = null;

            if (groupName == "生产部") {
                //先将不是分摊的统计出来
                list = result.Where(r => r.x.is_share_fee == false).GroupBy(r => r.x.dept_name).Select(r => new stringDecimalModel { name = r.Key, value = r.Sum(a => a.s.price) }).ToList();

                //分摊的需要逐条计算
                foreach (var r in result.Where(r => r.x.is_share_fee == true)) {
                    var shares = JsonConvert.DeserializeObject<List<XAFeeShareModel>>(r.x.share_fee_detail);

                    foreach (var s in shares) {
                        var tmpFee = Math.Round((decimal)(s.v * r.s.price / 100.0m), 2);
                        var tmpName = s.n.Split(new char[] { '_' })[1];
                        var item = list.Where(l => l.name == tmpName).FirstOrDefault();
                        if (item == null) {
                            list.Add(new stringDecimalModel() { name = tmpName, value = tmpFee });
                        }
                        else {
                            item.value += tmpFee;
                        }
                    }
                }
            }
            else if (groupName == "供应商") {
                list = result.GroupBy(r => r.s.supplier_name).Select(r => new stringDecimalModel { name = r.Key, value = r.Sum(a => a.s.price) }).ToList();
            }
            return Json(list.OrderByDescending(l => l.value).ToList());
        }

        //按生产部门查看明细
        public ActionResult CheckXASummaryDetailByDepName(string year, string month, bool? isPO, string depName)
        {
            DateTime fromDate, toDate;
            if ("整年".Equals(month)) {
                fromDate = DateTime.Parse(year + "-01-01");
                toDate = fromDate.AddYears(1);
            }
            else {
                fromDate = DateTime.Parse(year + "-" + month + "-01");
                toDate = fromDate.AddMonths(1);
            }

            var result = (from x in db.ei_xaApply
                          join s in db.ei_xaApplySupplier on new { x.sys_no, is_bidder = true } equals new { s.sys_no, s.is_bidder }
                          where x.confirm_date != null && x.confirm_date >= fromDate && x.confirm_date < toDate
                          && (isPO == null || x.is_po == isPO)
                          && ((!x.is_share_fee && x.dept_name == depName) || (x.is_share_fee && x.share_fee_detail.Contains(depName)))
                          select new
                          {
                              x,
                              s
                          }).ToList();

            
            List<XASummaryDetailModel> list = new List<XASummaryDetailModel>();
            //先加入不是分摊的
            foreach(var r in result.Where(re=>re.x.is_share_fee==false).OrderBy(re=>re.x.confirm_date)){
                list.Add(new XASummaryDetailModel()
                {
                    申请人 = r.x.applier_name,
                    申请部门 = r.x.dept_name,
                    是否PO = r.x.is_po == true ? "是" : (r.x.is_po == false ? "否" : ""),
                    确认日期 = ((DateTime)r.x.confirm_date).ToString("yyyy-MM-dd HH:mm"),
                    价格 = ((decimal)r.s.price).ToString("###,###.##"),
                    项目名称 = r.x.project_name,
                    中标供应商 = r.s.supplier_name,
                    流水号 = r.x.sys_no,
                    是否分摊单 = "否"
                });
            }

            //再加入分摊的
            foreach (var r in result.Where(r => r.x.is_share_fee == true).OrderBy(r => r.x.confirm_date)) {
                var shares = JsonConvert.DeserializeObject<List<XAFeeShareModel>>(r.x.share_fee_detail);

                foreach (var s in shares.Where(s=>s.n.Contains(depName))) {
                    var tmpFee = Math.Round((decimal)(s.v * r.s.price / 100.0m), 2);

                    list.Add(new XASummaryDetailModel()
                    {
                        申请人 = r.x.applier_name,
                        申请部门 = r.x.dept_name,
                        是否PO = r.x.is_po == true ? "是" : (r.x.is_po == false ? "否" : ""),
                        确认日期 = ((DateTime)r.x.confirm_date).ToString("yyyy-MM-dd HH:mm"),
                        价格 = tmpFee.ToString("###,###.##"),
                        项目名称 = r.x.project_name,
                        中标供应商 = r.s.supplier_name,
                        流水号 = r.x.sys_no,
                        是否分摊单 = "是"
                    });
                }
            }

            TempData["title"] = string.Format("项目申请单明细({0}_{1}-{2})", depName, year, month);
            TempData["json"] = JsonConvert.SerializeObject(list);
            return RedirectToAction("JsonTable", "BI");
        }

        //按供应商查看明细
        public ActionResult CheckXASummaryDetailBySupplierName(string year, string month, bool? isPO, string supplierName)
        {
            DateTime fromDate, toDate;
            if ("整年".Equals(month)) {
                fromDate = DateTime.Parse(year + "-01-01");
                toDate = fromDate.AddYears(1);
            }
            else {
                fromDate = DateTime.Parse(year + "-" + month + "-01");
                toDate = fromDate.AddMonths(1);
            }

            var result = (from x in db.ei_xaApply
                          join s in db.ei_xaApplySupplier on new { x.sys_no, is_bidder = true } equals new { s.sys_no, s.is_bidder }
                          where x.confirm_date != null && x.confirm_date >= fromDate && x.confirm_date < toDate
                          && (isPO == null || x.is_po == isPO)
                          && s.supplier_name == supplierName
                          select new
                          {
                              x,
                              s
                          }).ToList();


            List<XASummaryDetailModel> list = new List<XASummaryDetailModel>();            
            foreach (var r in result.OrderBy(re => re.x.confirm_date)) {
                list.Add(new XASummaryDetailModel()
                {
                    申请人 = r.x.applier_name,
                    申请部门 = r.x.dept_name,
                    是否PO = r.x.is_po == true ? "是" : (r.x.is_po == false ? "否" : ""),
                    确认日期 = ((DateTime)r.x.confirm_date).ToString("yyyy-MM-dd HH:mm"),
                    价格 = ((decimal)r.s.price).ToString("###,###.##"),
                    项目名称 = r.x.project_name,
                    中标供应商 = r.s.supplier_name,
                    流水号 = r.x.sys_no,
                    是否分摊单 = r.x.is_share_fee == true ? "是" : (r.x.is_po == false ? "否" : "")
                });
            }

            TempData["title"] = string.Format("项目申请单明细({0}_{1}-{2})", supplierName, year, month);
            TempData["json"] = JsonConvert.SerializeObject(list);
            return RedirectToAction("JsonTable", "BI");
        }

        #endregion

        #region 设备类申请单
        public ActionResult XBReport()
        {
            return View();
        }

        public JsonResult SearchXBReport(string obj)
        {
            XBSearchReportModel m = JsonConvert.DeserializeObject<XBSearchReportModel>(obj);
            m.toDate = m.toDate.AddDays(1);

            var result = from x in db.ei_xbApply
                         join a in db.flow_apply on x.sys_no equals a.sys_no
                         join aet in db.flow_applyEntry on new { id = a.id, pass = (bool?)null } equals new { id = (int)aet.apply_id, pass = aet.pass } into aettemp
                         from ae in aettemp.DefaultIfEmpty()
                         where x.apply_time >= m.fromDate
                         && x.apply_time < m.toDate
                         select new
                         {
                             sysNo = x.sys_no,
                             auditStatus = a.user_abort == true ? "撤销" : (a.success == true ? "已通过" : (a.success == false ? "已拒绝" : ae.step_name)),
                             applierName = x.applier_name,
                             applyTime = x.apply_time,
                             equitmentName = x.property_name,
                             dealType = x.deal_type,
                             billNo = x.bill_no,
                             company = x.company
                         };

            //var buyerRelation = db.flow_auditorRelation.Where(f => f.bill_type == "XA" && f.relate_name == "采购部审批" && f.relate_value == userInfo.cardNo).ToList();
            //if (buyerRelation.Count() > 0) {
            //    var companyList = buyerRelation.Select(b => b.relate_text).ToList();
            //    result = result.Where(r => companyList.Contains(r.company));
            //}

            if (!string.IsNullOrEmpty(m.dealType)) {
                result = result.Where(r => r.dealType == m.dealType);
            }
            if (!string.IsNullOrEmpty(m.applierName)) {
                result = result.Where(r => r.applierName.Contains(m.applierName));
            }
            if (!string.IsNullOrEmpty(m.sysNo)) {
                result = result.Where(r => r.sysNo.Contains(m.sysNo));
            }
            if (!string.IsNullOrEmpty(m.equitmentName)) {
                result = result.Where(r => r.equitmentName.Contains(m.equitmentName));
            }

            return Json(result.Distinct().ToList());
        }

        public void ExportXBExcel(string obj)
        {
            XBSearchReportModel m = JsonConvert.DeserializeObject<XBSearchReportModel>(obj);
            m.toDate = m.toDate.AddDays(1);

            var result = from x in db.ei_xbApply
                         join a in db.flow_apply on x.sys_no equals a.sys_no
                         join st in db.ei_xaApplySupplier on x.sys_no equals st.sys_no into stemp
                         from s in stemp.DefaultIfEmpty()
                         join aet in db.flow_applyEntry on new { id = a.id, pass = (bool?)null } equals new { id = (int)aet.apply_id, pass = aet.pass } into aettemp
                         from ae in aettemp.DefaultIfEmpty()
                         where x.apply_time >= m.fromDate
                         && x.apply_time < m.toDate
                         select new
                         {
                             x,
                             s,
                             auditResult = a.user_abort == true ? "撤销" : (a.success == true ? "已通过" : (a.success == false ? "已拒绝" : ae.step_name)),
                         };

            //var buyerRelation = db.flow_auditorRelation.Where(f => f.bill_type == "XA" && f.relate_name == "采购部审批" && f.relate_value == userInfo.cardNo).ToList();
            //if (buyerRelation.Count() > 0) {
            //    var companyList = buyerRelation.Select(b => b.relate_text).ToList();
            //    result = result.Where(r => companyList.Contains(r.x.company));
            //}

            if (!string.IsNullOrEmpty(m.dealType)) {
                result = result.Where(r => r.x.deal_type == m.dealType);
            }
            if (!string.IsNullOrEmpty(m.applierName)) {
                result = result.Where(r => r.x.applier_name.Contains(m.applierName));
            }
            if (!string.IsNullOrEmpty(m.sysNo)) {
                result = result.Where(r => r.x.sys_no.Contains(m.sysNo));
            }
            if (!string.IsNullOrEmpty(m.equitmentName)) {
                result = result.Where(r => r.x.property_name.Contains(m.equitmentName));
            }

            string[] colName = new string[] { "处理结果","申请流水号", "申请人", "联系电话", "申请时间","公司", "申请部门", "项目编号", "处理类别","地点",
                                              "设备名称","设备编号","设备型号","设备原价值","启用日期","闲置时间","设备卡片号","产地/供应商","设备其它信息",
                                              "申请原因", "具体要求", "项目收益","人员节省","战略性投资","产能收益","其它收益", "中标供应商","中标价格" };
            ushort[] colWidth = new ushort[colName.Length];

            for (var i = 0; i < colWidth.Length; i++) {
                colWidth[i] = 16;
            }

            //設置excel文件名和sheet名
            XlsDocument xls = new XlsDocument();
            xls.FileName = "设备类申请单列表_" + DateTime.Now.ToString("MMddHHmmss");
            Worksheet sheet = xls.Workbook.Worksheets.Add("申请详情");

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

            var data = result.ToList();
            foreach (var d in data.Select(r => r.x).Distinct().ToList()) {
                colIndex = 1;

                //"处理结果","申请流水号", "申请人", "联系电话", "申请时间","公司", "申请部门", "项目编号", "处理类别","地点",
                //"设备名称","设备编号","设备型号","设备原价值","启用日期","闲置时间","设备卡片号","产地/供应商","设备其它信息",
                //"申请原因", "具体要求", "项目收益","人员节省","战略性投资","产能收益","其它收益", "中标供应商","中标价格"
                cells.Add(++rowIndex, colIndex, data.Where(a => a.x == d).First().auditResult);
                cells.Add(rowIndex, ++colIndex, d.sys_no);
                cells.Add(rowIndex, ++colIndex, d.applier_name);
                cells.Add(rowIndex, ++colIndex, d.applier_phone);
                cells.Add(rowIndex, ++colIndex, d.apply_time.ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, d.company);
                cells.Add(rowIndex, ++colIndex, d.dept_name);
                cells.Add(rowIndex, ++colIndex, d.bill_no);
                cells.Add(rowIndex, ++colIndex, d.deal_type);
                cells.Add(rowIndex, ++colIndex, d.addr);

                cells.Add(rowIndex, ++colIndex, d.property_name);
                cells.Add(rowIndex, ++colIndex, d.property_number);
                cells.Add(rowIndex, ++colIndex, d.property_modual);
                cells.Add(rowIndex, ++colIndex, d.property_worth);
                cells.Add(rowIndex, ++colIndex, d.property_enable_date==null?"":((DateTime)d.property_enable_date).ToString("yyyy-MM-dd"));
                cells.Add(rowIndex, ++colIndex, d.property_idle_time);
                cells.Add(rowIndex, ++colIndex, d.property_card_no);
                cells.Add(rowIndex, ++colIndex, d.property_supplier);
                cells.Add(rowIndex, ++colIndex, d.property_other_comment);


                cells.Add(rowIndex, ++colIndex, d.reason);
                cells.Add(rowIndex, ++colIndex, d.demands.Replace("\br", ";"));
                cells.Add(rowIndex, ++colIndex, d.has_profit ? "有" : "无");
                cells.Add(rowIndex, ++colIndex, d.save_people);
                cells.Add(rowIndex, ++colIndex, d.strategy_profit);
                cells.Add(rowIndex, ++colIndex, d.productivity_profit);
                cells.Add(rowIndex, ++colIndex, d.other_profit);
                if (data.Where(a => a.x == d && a.s != null).Count() > 0) {
                    cells.Add(rowIndex, ++colIndex, data.Where(a => a.x == d && a.s.is_bidder).Select(a => a.s.supplier_name).FirstOrDefault());
                    cells.Add(rowIndex, ++colIndex, data.Where(a => a.x == d && a.s.is_bidder).Select(a => a.s.price).FirstOrDefault());
                }
            }

            xls.Send();

        }
        #endregion

        #region 委外加工

        //放行条
        //[SessionTimeOutFilter]
        //public ActionResult PrintXC(string sysNo)
        //{
        //    var m = (from x in db.ei_xcApply
        //             join e in db.ei_xcMatOutDetail on x.sys_no equals e.sys_no
        //             where x.sys_no == sysNo
        //             select new
        //             {
        //                 bill = x,
        //                 mats = e
        //             }).ToList();

        //    if (m.Count() < 1) {
        //        ViewBag.tip = "不存在需要打印的数据";
        //        return View("Error");
        //    }

        //    var bill = m.First().bill;
        //    if (bill.out_time != null) {
        //        ViewBag.tip = "此放行条已放行，不能再打印";
        //        return View("Error");
        //    }
        //    if (bill.total_price == null) {
        //        ViewBag.tip = "此放行条采购未下单或被拒绝，不能打印";
        //        return View("Error"); 
        //    }


        //    new XCSv(sysNo).UpdatePrintStatus();

        //    ViewData["m"] = new XCCheckApplyModel()
        //    {
        //        bill = bill,
        //        mats = m.Select(a => a.mats).ToList(),
        //        auditorList = GetAuditorList(sysNo)
        //    };

        //    ViewData["printer"] = userInfo.name;

        //    return View();
        //}

        [SessionTimeOutFilter]
        public ActionResult XCReport()
        {
            return View();
        }

        public JsonResult SearchXCReport(string obj)
        {
            XCSearchReportModel m = JsonConvert.DeserializeObject<XCSearchReportModel>(obj);
            m.toDate = m.toDate.AddDays(1);

            var result = from x in db.ei_xcApply
                         join e in db.ei_xcProduct on x.sys_no equals e.sys_no
                         join a in db.flow_apply on x.sys_no equals a.sys_no
                         join aet in db.flow_applyEntry on new { id = a.id, pass = (bool?)null } equals new { id = (int)aet.apply_id, pass = aet.pass } into aettemp
                         from ae in aettemp.DefaultIfEmpty()
                         where x.apply_time >= m.fromDate
                         && x.apply_time < m.toDate
                         select new
                         {
                             sysNo = x.sys_no,
                             auditStatus = a.user_abort == true ? "撤销" : (a.success == true ? "已通过" : (a.success == false ? "已拒绝" : ae.step_name)),
                             applierName = x.applier_name,
                             applyTime = x.apply_time,
                             depName = x.dep_name,
                             productModel = e.product_model,
                             qty = e.qty
                         };

            var canCheckBus = db.ei_flowAuthority.Where(f => f.bill_type == "XC" && f.relate_type == "查询报表" && f.relate_value == userInfo.cardNo).FirstOrDefault();
            if (canCheckBus != null && !string.IsNullOrEmpty(canCheckBus.cond1)) {
                var buses = canCheckBus.cond1.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                result = result.Where(r => buses.Contains(r.depName));
            }
            
            if (!string.IsNullOrEmpty(m.applierName)) {
                result = result.Where(r => r.applierName.Contains(m.applierName));
            }
            if (!string.IsNullOrEmpty(m.sysNo)) {
                result = result.Where(r => r.sysNo.Contains(m.sysNo));
            }
            if (!string.IsNullOrEmpty(m.depName)) {
                result = result.Where(r => r.depName.Contains(m.depName));
            }
            if (!string.IsNullOrEmpty(m.productModel)) {
                result = result.Where(r => r.productModel.Contains(m.productModel));
            }


            return Json(result.Distinct().ToList());

        }

        public void ExportXCExcel(string obj)
        {
            XCSearchReportModel m = JsonConvert.DeserializeObject<XCSearchReportModel>(obj);
            m.toDate = m.toDate.AddDays(1);

            var result = from x in db.ei_xcApply
                         join e in db.ei_xcProduct on x.sys_no equals e.sys_no
                         join a in db.flow_apply on x.sys_no equals a.sys_no
                         join aet in db.flow_applyEntry on new { id = a.id, pass = (bool?)null } equals new { id = (int)aet.apply_id, pass = aet.pass } into aettemp
                         from ae in aettemp.DefaultIfEmpty()
                         where x.apply_time >= m.fromDate
                         && x.apply_time < m.toDate
                         select new
                         {
                             bill = x,
                             pro = e,
                             auditStatus = a.user_abort == true ? "撤销" : (a.success == true ? "已通过" : (a.success == false ? "已拒绝" : ae.step_name))
                         };

            var canCheckBus = db.ei_flowAuthority.Where(f => f.bill_type == "XC" && f.relate_type == "查询报表" && f.relate_value == userInfo.cardNo).FirstOrDefault();
            if (canCheckBus != null && !string.IsNullOrEmpty(canCheckBus.cond1)) {
                var buses = canCheckBus.cond1.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                result = result.Where(r => buses.Contains(r.bill.dep_name));
            }

            if (!string.IsNullOrEmpty(m.applierName)) {
                result = result.Where(r => r.bill.applier_name.Contains(m.applierName));
            }
            if (!string.IsNullOrEmpty(m.sysNo)) {
                result = result.Where(r => r.bill.sys_no.Contains(m.sysNo));
            }
            if (!string.IsNullOrEmpty(m.depName)) {
                result = result.Where(r => r.bill.dep_name.Contains(m.depName));
            }
            if (!string.IsNullOrEmpty(m.productModel)) {
                result = result.Where(r => r.pro.product_model.Contains(m.productModel));
            }

            string[] colName = new string[] { "处理结果","申请流水号", "申请人", "联系电话", "申请时间", "公司", "部门", "已用额度", "目标额度", "地点", 
                                              "本次委外加工天数", "预计加工完成时间","备注",
                                              "计划部负责人","部门主管","采购员", "供应商","采购单号","行号","产品编码","产品名称","产品型号","加工数量","单价", "总价" };
            ushort[] colWidth = new ushort[colName.Length];

            for (var i = 0; i < colWidth.Length; i++) {
                colWidth[i] = 16;
            }

            //設置excel文件名和sheet名
            XlsDocument xls = new XlsDocument();
            xls.FileName = "委外加工申请列表_" + DateTime.Now.ToString("MMddHHmmss");
            Worksheet sheet = xls.Workbook.Worksheets.Add("申请详情");

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

            var data = result.ToList();
            foreach (var d in data.Select(r => r.bill).Distinct().OrderBy(da=>da.apply_time).ToList()) {
                colIndex = 1;

                //"处理结果","申请流水号", "申请人", "联系电话", "申请时间", "公司", "部门", "已用额度", "目标额度", "地点", "采购单号",
                //"本次委外加工天数", "预计加工完成时间","备注",
                //"计划部负责人","部门主管","采购员", "供应商","产品编码","产品名称","产品型号","加工数量","单价", "总价"
                cells.Add(++rowIndex, colIndex, data.Where(a => a.bill == d).First().auditStatus);
                cells.Add(rowIndex, ++colIndex, d.sys_no);
                cells.Add(rowIndex, ++colIndex, d.applier_name);
                cells.Add(rowIndex, ++colIndex, d.applier_phone);
                cells.Add(rowIndex, ++colIndex, d.apply_time.ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, d.company);
                cells.Add(rowIndex, ++colIndex, d.dep_name);
                cells.Add(rowIndex, ++colIndex, d.current_month_total);
                cells.Add(rowIndex, ++colIndex, d.current_month_target);
                cells.Add(rowIndex, ++colIndex, d.addr);

                cells.Add(rowIndex, ++colIndex, d.outsourcing_cycle);
                cells.Add(rowIndex, ++colIndex, d.estimate_finish_time.ToString("yyyy-MM-dd"));
                cells.Add(rowIndex, ++colIndex, d.comment);

                cells.Add(rowIndex, ++colIndex, d.planner_auditor);
                cells.Add(rowIndex, ++colIndex, d.dep_charger);
                cells.Add(rowIndex, ++colIndex, d.buyer_auditor);
                cells.Add(rowIndex, ++colIndex, d.supplier_name);
                cells.Add(rowIndex, ++colIndex, d.bus_po_no);

                int cIndex;
                foreach (var p in data.Where(a => a.bill == d).Select(a => a.pro).Distinct().ToList()) {
                    cIndex = colIndex;                    
                    cells.Add(rowIndex, ++cIndex, p.entry_id);
                    cells.Add(rowIndex, ++cIndex, p.product_no);
                    cells.Add(rowIndex, ++cIndex, p.product_name);
                    cells.Add(rowIndex, ++cIndex, p.product_model);
                    cells.Add(rowIndex, ++cIndex, p.qty);
                    cells.Add(rowIndex, ++cIndex, p.unit_price);
                    cells.Add(rowIndex, ++cIndex, p.total_price);
                    rowIndex++;
                }
                rowIndex--;
                
            }

            xls.Send();
        }
        

        #endregion

        #region 委外超时

        [SessionTimeOutFilter]
        public ActionResult XDReport()
        {
            return View();
        }

        public JsonResult SearchXDReport(string depName, DateTime fromDate, DateTime toDate)
        {
            toDate = toDate.AddDays(1);

            var result = (from d in db.ei_xdApply
                          join a in db.flow_apply on d.sys_no equals a.sys_no
                          join aet in db.flow_applyEntry on new { id = a.id, pass = (bool?)null } equals new { id = (int)aet.apply_id, pass = aet.pass } into aettemp
                          from ae in aettemp.DefaultIfEmpty()
                          where d.process_dep.Contains(depName)
                          && d.time_from >= fromDate
                          && d.time_to <= toDate
                          orderby d.time_from
                          select new
                          {
                              auditStatus = a.user_abort == true ? "撤销" : (a.success == true ? "已通过" : (a.success == false ? "已拒绝" : ae.step_name)),
                              d.process_dep,
                              d.time_from,
                              d.time_to,
                              d.sys_no,
                              d.applier_name,
                              d.pro_type
                          }).ToList();

            return Json(result);
        }

        public void ExportXDExcel(string depName, DateTime fromDate, DateTime toDate)
        {
            toDate = toDate.AddDays(1);

            var result = (from d in db.ei_xdApply
                          join a in db.flow_apply on d.sys_no equals a.sys_no
                          join aet in db.flow_applyEntry on new { id = a.id, pass = (bool?)null } equals new { id = (int)aet.apply_id, pass = aet.pass } into aettemp
                          from ae in aettemp.DefaultIfEmpty()
                          where d.process_dep.Contains(depName)
                          && d.time_from >= fromDate
                          && d.time_to <= toDate
                          orderby d.time_from
                          select new
                          {
                              auditStatus = a.user_abort == true ? "撤销" : (a.success == true ? "已通过" : (a.success == false ? "已拒绝" : ae.step_name)),
                              h = d
                          }).ToList();

            string[] colName = new string[] { "审批结果","申请流水号", "申请人", "申请时间", "申请类型", "申请部门", "超时开始时间", "超时结束时间","K3帐套", "订单号",
                                              "供应商", "卡板数", "部门负责人", "仓库确认人","物流确认人","超时原因" };
            ushort[] colWidth = new ushort[colName.Length];

            for (var i = 0; i < colWidth.Length; i++) {
                colWidth[i] = 16;
            }

            //設置excel文件名和sheet名
            XlsDocument xls = new XlsDocument();
            xls.FileName = "委外超时申请列表_" + DateTime.Now.ToString("MMddHHmmss");
            Worksheet sheet = xls.Workbook.Worksheets.Add("申请详情");

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

            foreach (var d in result.ToList()) {
                colIndex = 1;

                //"审批结果","申请流水号", "申请人", "申请时间", "申请类型", "申请部门", "超时开始时间", "超时结束时间","K3帐套", "订单号",
                //"供应商", "卡板数", "部门负责人", "仓库确认人","物流确认人","超时原因"
                cells.Add(++rowIndex, colIndex, d.auditStatus);
                cells.Add(rowIndex, ++colIndex, d.h.sys_no);
                cells.Add(rowIndex, ++colIndex, d.h.applier_name);
                cells.Add(rowIndex, ++colIndex, d.h.apply_time.ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, d.h.pro_type);
                cells.Add(rowIndex, ++colIndex, d.h.process_dep);
                cells.Add(rowIndex, ++colIndex, d.h.time_from.ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, d.h.time_to.ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, d.h.k3_account);
                cells.Add(rowIndex, ++colIndex, d.h.order_no);

                cells.Add(rowIndex, ++colIndex, d.h.supplier_name);
                cells.Add(rowIndex, ++colIndex, d.h.cardborad_num);
                cells.Add(rowIndex, ++colIndex, d.h.dep_charger);
                cells.Add(rowIndex, ++colIndex, d.h.stock_confirm_people);
                cells.Add(rowIndex, ++colIndex, d.h.lod_confirm_people);
                cells.Add(rowIndex, ++colIndex, d.h.reason);
            }

            xls.Send();

        }


        #endregion

        #region 设备保养

        [SessionTimeOutFilter]
        public ActionResult MTReport()
        {
            return View();
        }

        public JsonResult SearchMTReport(DateTime fromDate, DateTime toDate, string accepterName)
        {
            toDate = toDate.AddDays(1);

            bool canSeeAll = db.ei_flowAuthority.Where(f => f.bill_type == "MT" && f.relate_type == "查看所有设备" && f.relate_value == userInfo.cardNo).Count() > 0;
            var result = (from d in db.ei_mtApply
                          join i in db.ei_mtEqInfo on d.eqInfo_id equals i.id
                          join c in db.ei_mtClass on i.class_id equals c.id
                          join a in db.flow_apply on d.sys_no equals a.sys_no
                          join aet in db.flow_applyEntry on new { id = a.id, pass = (bool?)null } equals new { id = (int)aet.apply_id, pass = aet.pass } into aettemp
                          from ae in aettemp.DefaultIfEmpty()
                          where d.apply_time >= fromDate
                          && d.apply_time < toDate
                          && (c.leader_number.Contains(userInfo.cardNo) || canSeeAll)
                          orderby d.apply_time, d.accept_time
                          select new
                          {
                              auditStatus = a.user_abort == true ? "撤销" : (a.success == true ? "已办结" : (a.success == false ? "已拒绝" : ae.step_name)),
                              d.accept_member_name,
                              d.apply_time,
                              i.produce_dep_name,
                              c.class_name,
                              d.sys_no,
                              i.equitment_name,
                              i.equitment_modual
                          });
            if (!string.IsNullOrEmpty(accepterName)) {
                result = result.Where(r => r.accept_member_name.Contains(accepterName));
            }

            return Json(result.ToList());

        }

        public void ExportMTExcel(DateTime fromDate, DateTime toDate, string accepterName)
        {
            toDate = toDate.AddDays(1);

            bool canSeeAll = db.ei_flowAuthority.Where(f => f.bill_type == "MT" && f.relate_type == "查看所有设备" && f.relate_value == userInfo.cardNo).Count() > 0;
            var result = (from d in db.ei_mtApply
                          join i in db.ei_mtEqInfo on d.eqInfo_id equals i.id
                          join c in db.ei_mtClass on i.class_id equals c.id
                          join a in db.flow_apply on d.sys_no equals a.sys_no
                          join aet in db.flow_applyEntry on new { id = a.id, pass = (bool?)null } equals new { id = (int)aet.apply_id, pass = aet.pass } into aettemp
                          from ae in aettemp.DefaultIfEmpty()
                          where d.apply_time >= fromDate
                          && d.apply_time < toDate
                          && (c.leader_number.Contains(userInfo.cardNo) || canSeeAll)
                          orderby d.apply_time, d.accept_time
                          select new
                          {
                              auditStatus = a.user_abort == true ? "撤销" : (a.success == true ? "已通过" : (a.success == false ? "已拒绝" : ae.step_name)),
                              h = d,
                              i = i,
                              c = c
                          });
            if (!string.IsNullOrEmpty(accepterName)) {
                result = result.Where(r => r.h.accept_member_name.Contains(accepterName));
            }

            string[] colName = new string[] { "审批结果","申请流水号","科室名称", "发起人", "发起时间", "资产编号", "设备名称", "规格型号", "制造商","生产部门", "接单人",
                                              "接单日期", "保养开始时间", "保养结束时间", "保养花费时间","处理时间","协助人","确认时间","备注" };
            ushort[] colWidth = new ushort[colName.Length];

            for (var i = 0; i < colWidth.Length; i++) {
                colWidth[i] = 16;
            }

            //設置excel文件名和sheet名
            XlsDocument xls = new XlsDocument();
            xls.FileName = "设备保养列表_" + DateTime.Now.ToString("MMddHHmmss");
            Worksheet sheet = xls.Workbook.Worksheets.Add("保养详情");

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

            foreach (var d in result.ToList()) {
                colIndex = 1;

                //"审批结果","申请流水号", "发起人", "发起时间", "资产编号", "设备名称", "规格型号", "制造商","生产部门", "接单人",
                //"接单日期", "保养开始时间", "保养结束时间", "保养花费时间","处理时间","协助人","确认时间","备注"
                cells.Add(++rowIndex, colIndex, d.auditStatus);
                cells.Add(rowIndex, ++colIndex, d.h.sys_no);
                cells.Add(rowIndex, ++colIndex, d.c.class_name);
                cells.Add(rowIndex, ++colIndex, d.h.applier_name);
                cells.Add(rowIndex, ++colIndex, d.h.apply_time.ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, d.i.property_number);
                cells.Add(rowIndex, ++colIndex, d.i.equitment_name);
                cells.Add(rowIndex, ++colIndex, d.i.equitment_modual);
                cells.Add(rowIndex, ++colIndex, d.i.maker);
                cells.Add(rowIndex, ++colIndex, d.i.produce_dep_name);
                cells.Add(rowIndex, ++colIndex, d.h.accept_member_name);

                cells.Add(rowIndex, ++colIndex, d.h.accept_time == null ? "" : ((DateTime)d.h.accept_time).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, d.h.maintence_begin_time == null ? "" : ((DateTime)d.h.maintence_begin_time).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, d.h.maintence_end_time == null ? "" : ((DateTime)d.h.maintence_end_time).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, d.h.maintence_hours + " 小时");
                cells.Add(rowIndex, ++colIndex, d.h.maintence_time == null ? "" : ((DateTime)d.h.maintence_time).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, d.h.member_helping);
                cells.Add(rowIndex, ++colIndex, d.h.confirm_time == null ? "" : ((DateTime)d.h.confirm_time).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, d.h.comment);
            }

            xls.Send();

        }
        
        public void ExportMyMTEqInfo(string className = "", string eqName = "", string fileNo = "")
        {
            bool canSeeAll = db.ei_flowAuthority.Where(f => f.bill_type == "MT" && f.relate_type == "查看所有设备" && f.relate_value == userInfo.cardNo).Count() > 0;
            var list = from eq in db.ei_mtEqInfo
                       join c in db.ei_mtClass on eq.class_id equals c.id
                       where c.leader_number.Contains(userInfo.cardNo) || eq.creater_number == userInfo.cardNo || canSeeAll
                       orderby eq.maintenance_status
                       select new
                       {
                           eq,
                           c
                       };

            if (!string.IsNullOrEmpty(className)) {
                list = list.Where(l => l.c.class_name.Contains(className));
            }
            if (!string.IsNullOrEmpty(eqName)) {
                list = list.Where(l => l.eq.equitment_name.Contains(eqName));
            }
            if (!string.IsNullOrEmpty(fileNo)) {
                list = list.Where(l => l.eq.file_no.Contains(fileNo));
            }

            var result = list.ToList().Where(l => l.eq.maintenance_status == "正在保养").OrderBy(l => l.eq.next_maintenance_date).ToList();
            result.AddRange(list.ToList().Where(l => l.eq.maintenance_status != "正在保养").OrderBy(l => l.eq.maintenance_status).ThenBy(l => l.eq.next_maintenance_date).ToList());

            string[] colName = new string[] { "保养状态","设备科室","资产编号","设备名称", "设备型号", "生产部门", "保养文件", "制造商", "使用状态", "盘点部门", "盘点表编号",
                                              "优先处理级别", "保养周期(月)", "上次保养日期", "下次保养日期", "创建人","创建时间" };
            ushort[] colWidth = new ushort[colName.Length];

            for (var i = 0; i < colWidth.Length; i++) {
                colWidth[i] = 16;
            }

            //設置excel文件名和sheet名
            XlsDocument xls = new XlsDocument();
            xls.FileName = "设备信息列表_" + DateTime.Now.ToString("MMddHHmmss");
            Worksheet sheet = xls.Workbook.Worksheets.Add("设备详情");

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

            foreach (var d in result.ToList()) {
                colIndex = 1;

                //"保养状态","设备科室","资产编号","设备名称", "设备型号", "生产部门", "保养文件", "制造商", "使用状态", "盘点部门", "盘点表编号",
                //"优先处理级别", "保养周期(月)", "上次保养日期", "下次保养日期", "创建人","创建时间"
                cells.Add(++rowIndex, colIndex, d.eq.maintenance_status);
                cells.Add(rowIndex, ++colIndex, d.c.class_name);
                cells.Add(rowIndex, ++colIndex, d.eq.property_number);
                cells.Add(rowIndex, ++colIndex, d.eq.equitment_name);
                cells.Add(rowIndex, ++colIndex, d.eq.equitment_modual);
                cells.Add(rowIndex, ++colIndex, d.eq.produce_dep_name);
                cells.Add(rowIndex, ++colIndex, d.eq.file_no);
                cells.Add(rowIndex, ++colIndex, d.eq.maker);
                cells.Add(rowIndex, ++colIndex, d.eq.using_status);
                cells.Add(rowIndex, ++colIndex, d.eq.check_dep);
                cells.Add(rowIndex, ++colIndex, d.eq.check_list_no);

                cells.Add(rowIndex, ++colIndex, d.eq.important_level);
                cells.Add(rowIndex, ++colIndex, d.eq.maintenance_cycle);
                cells.Add(rowIndex, ++colIndex, d.eq.last_maintenance_date == null ? "" : ((DateTime)d.eq.last_maintenance_date).ToString("yyyy-MM-dd"));
                cells.Add(rowIndex, ++colIndex, d.eq.next_maintenance_date == null ? "" : ((DateTime)d.eq.next_maintenance_date).ToString("yyyy-MM-dd"));
                cells.Add(rowIndex, ++colIndex, d.eq.creater_name);
                cells.Add(rowIndex, ++colIndex, d.eq.create_time.ToString("yyyy-MM-dd HH:mm"));
            }

            xls.Send();

        }

        #endregion

        #region 放行条流程

        [SessionTimeOutFilter]
        public ActionResult FXReport()
        {
            if (db.ei_flowAuthority.Where(f => f.bill_type == "FX" && f.relate_type == "查询报表" && f.relate_value == userInfo.cardNo).Count() < 1) {
                ViewBag.tip = "没有查询权限";
                return View("Error");
            }
            return View();
        }

        public JsonResult SearchFXReport(string obj)
        {
            var m = JsonConvert.DeserializeObject<FXSearchReportModel>(obj);
            m.toDate = m.toDate.AddDays(1);

            var result = from x in db.ei_fxApply
                         //join e in db.ei_fxApplyEntry on x.sys_no equals e.sys_no
                         join a in db.flow_apply on x.sys_no equals a.sys_no
                         join aet in db.flow_applyEntry on new { id = a.id, pass = (bool?)null } equals new { id = (int)aet.apply_id, pass = aet.pass } into aettemp
                         from ae in aettemp.DefaultIfEmpty()
                         where x.apply_time >= m.fromDate
                         && x.apply_time < m.toDate
                         select new
                         {
                             sysNo = x.sys_no,
                             auditStatus = x.out_time == null ? (a.user_abort == true ? "撤销" : (a.success == true ? "已通过" : (a.success == false ? "已拒绝" : ae.step_name))) : x.out_status,
                             applierName = x.applier_name,
                             applyTime = x.apply_time,
                             busName = x.bus_name,
                             //itemName = e.item_name,
                             //itemModel = e.item_model,
                             //qty = e.item_qty,
                             typeNo = x.fx_type_no,
                             typeName = x.fx_type_name
                         };

            if (!string.IsNullOrEmpty(m.applierName)) {
                result = result.Where(r => r.applierName.Contains(m.applierName));
            }

            if (!string.IsNullOrEmpty(m.auditStatus)) {
                result = result.Where(r => r.auditStatus == m.auditStatus);
            }

            if (!string.IsNullOrEmpty(m.busName)) {
                result = result.Where(r => r.busName.Contains(m.busName));
            }

            if (!string.IsNullOrEmpty(m.typeNo)) {
                result = result.Where(r => r.typeNo.StartsWith(m.typeNo));
            }

            if (!string.IsNullOrEmpty(m.typeName)) {
                result = result.Where(r => r.typeName.Contains(m.typeName));
            }

            if (!string.IsNullOrEmpty(m.sysNo)) {
                result = result.Where(r => r.sysNo.Contains(m.sysNo));
            }

            return Json(result.ToList());
        }

        public void ExportFXExcel(string obj)
        {
            var m = JsonConvert.DeserializeObject<FXSearchReportModel>(obj);
            m.toDate = m.toDate.AddDays(1);

            var result = from x in db.ei_fxApply
                         join e in db.ei_fxApplyEntry on x.sys_no equals e.sys_no
                         join a in db.flow_apply on x.sys_no equals a.sys_no
                         join aet in db.flow_applyEntry on new { id = a.id, pass = (bool?)null } equals new { id = (int)aet.apply_id, pass = aet.pass } into aettemp
                         from ae in aettemp.DefaultIfEmpty()
                         where x.apply_time >= m.fromDate
                         && x.apply_time < m.toDate
                         select new
                         {
                             x,
                             e,
                             auditStatus = x.out_time == null ? (a.user_abort == true ? "撤销" : (a.success == true ? "已通过" : (a.success == false ? "已拒绝" : ae.step_name))) : x.out_status
                         };

            if (!string.IsNullOrEmpty(m.applierName)) {
                result = result.Where(r => r.x.applier_name.Contains(m.applierName));
            }

            if (!string.IsNullOrEmpty(m.auditStatus)) {
                result = result.Where(r => r.auditStatus == m.auditStatus);
            }

            if (!string.IsNullOrEmpty(m.busName)) {
                result = result.Where(r => r.x.bus_name.Contains(m.busName));
            }

            if (!string.IsNullOrEmpty(m.typeNo)) {
                result = result.Where(r => r.x.fx_type_no.StartsWith(m.typeNo));
            }

            if (!string.IsNullOrEmpty(m.typeName)) {
                result = result.Where(r => r.x.fx_type_name.Contains(m.typeName));
            }

            if (!string.IsNullOrEmpty(m.sysNo)) {
                result = result.Where(r => r.x.sys_no.Contains(m.sysNo));
            }

            string[] colName = new string[] { "审批结果","申请流水号","业务类型", "申请人", "申请时间", "公司", "部门", "件数", "放行厂区","寄/带出范围", "寄/带出地址",
                                              "申请原因", "备注", "物品名称", "物品型号","数量","单位" };
            ushort[] colWidth = new ushort[colName.Length];

            for (var i = 0; i < colWidth.Length; i++) {
                colWidth[i] = 16;
            }

            //設置excel文件名和sheet名
            XlsDocument xls = new XlsDocument();
            xls.FileName = "放行条列表_" + DateTime.Now.ToString("MMddHHmmss");
            Worksheet sheet = xls.Workbook.Worksheets.Add("详情");

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

            foreach (var d in result.ToList()) {
                colIndex = 1;

                //"审批结果","申请流水号","业务类型", "申请人", "申请时间", "公司", "部门", "件数", "放行厂区","寄/带出范围", "寄/带出地址",
                //"申请原因", "备注", "物品名称", "物品型号","数量","单位"
                cells.Add(++rowIndex, colIndex, d.auditStatus);
                cells.Add(rowIndex, ++colIndex, d.x.sys_no);
                cells.Add(rowIndex, ++colIndex, d.x.fx_type_name);
                cells.Add(rowIndex, ++colIndex, d.x.applier_name);
                cells.Add(rowIndex, ++colIndex, d.x.apply_time.ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, d.x.company);
                cells.Add(rowIndex, ++colIndex, d.x.bus_name);
                cells.Add(rowIndex, ++colIndex, d.x.total_pack_num);
                cells.Add(rowIndex, ++colIndex, d.x.from_addr);
                cells.Add(rowIndex, ++colIndex, d.x.to_scope);
                cells.Add(rowIndex, ++colIndex, d.x.to_addr);

                cells.Add(rowIndex, ++colIndex, d.x.reason);
                cells.Add(rowIndex, ++colIndex, d.x.comment);
                cells.Add(rowIndex, ++colIndex, d.e.item_name);
                cells.Add(rowIndex, ++colIndex, d.e.item_model);
                cells.Add(rowIndex, ++colIndex, d.e.item_qty);
                cells.Add(rowIndex, ++colIndex, d.e.item_unit);
            }

            xls.Send();

        }

        [SessionTimeOutFilter]
        public ActionResult PrintFX(string sysNo)
        {
            var fx = db.ei_fxApply.Where(f => f.sys_no == sysNo).FirstOrDefault();
            if (fx == null) {
                ViewBag.tip = "放行条不存在";
                return View("Error");
            }
            if (fx.out_time != null) {
                ViewBag.tip = "已放行不能打印";
                return View("Error");
            }
            if (!fx.can_print) {
                ViewBag.tip = "不能打印";
                return View("Error");
            }

            var entrys = db.ei_fxApplyEntry.Where(f => f.sys_no == sysNo).ToList();

            new FXSv(sysNo).UpdatePrintStatus();
            WriteEventLog("打印放行条", sysNo);

            ViewData["m"] = new FXCheckApplyModel()
            {
                bill = fx,
                entrys = entrys,
                auditorList = GetAuditorList(sysNo)
            };
            ViewData["printer"] = userInfo.name;

            return View();
        }


        [SessionTimeOutFilter]
        public ActionResult FXSummary()
        {
            return View();
        }

        public JsonResult SearchFXSummary(DateTime fromDate, DateTime toDate, string groupName, string fxTypeName)
        {
            toDate = toDate.AddDays(1);

            var result = db.ei_fxApply.Where(f => f.out_time >= fromDate && f.out_time < toDate && f.fx_type_name.Contains(fxTypeName)).ToList();

            List<stringDecimalModel> list = new List<stringDecimalModel>();
            if ("出货公司".Equals(groupName)) {
                list = result.Where(r => r.receiver_cop_name != null).GroupBy(r => r.receiver_cop_name).Select(r => new stringDecimalModel() { name = r.Key, value = r.Count() }).ToList();
            }
            if ("需返厂".Equals(groupName)) {
                list = result.Where(r => r.out_status == "未返厂" || r.out_status == "已返厂未确认").GroupBy(r => r.applier_name).Select(r => new stringDecimalModel() { name = r.Key, value = r.Count() }).ToList();
            }

            return Json(list.OrderByDescending(l => l.value).ThenBy(l => l.name).ToList());
        }

        public ActionResult CheckFXSummaryDetail(DateTime fromDate, DateTime toDate, string fxTypeName,string groupName, string groupValue)
        {
            toDate = toDate.AddDays(1);
            var result = db.ei_fxApply.Where(f => f.out_time >= fromDate && f.out_time < toDate && f.fx_type_name.Contains(fxTypeName)).ToList();
            if("出货公司".Equals(groupName)){
                result = result.Where(f => f.receiver_cop_name == groupValue).ToList();
            }
            if ("需返厂".Equals(groupName)) {
                result = result.Where(r => r.applier_name==groupValue && (r.out_status == "未返厂" || r.out_status == "已返厂未确认")).ToList();
            }

            var list = result.OrderBy(r => r.out_time).Select(r => new
            {
                流水号 = r.sys_no,
                申请人 = r.applier_name,
                部门 = r.bus_name,
                出货公司 = r.receiver_cop_name,
                业务类型 = r.fx_type_name,
                放行时间 = ((DateTime)r.out_time).ToString("yyyy-MM-dd HH:mm"),
                状态 = r.out_status
            });

            TempData["title"] = string.Format("放行申请单明细({0}_{1:yyyy-MM-dd}-{2:yyyy-MM-dd})", groupValue, fromDate, toDate);
            TempData["json"] = JsonConvert.SerializeObject(list);
            return RedirectToAction("JsonTable", "BI");

        }

        #endregion

        

    }
}
