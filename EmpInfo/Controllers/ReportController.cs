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
            }

            ViewData["sm"] = sm;
            ViewData["empLevels"] = db.ei_empLevel.OrderBy(m => m.level_no).ToList();
            return View();
        }

        private List<vw_askLeaveReport> GetALDatas(int depId, DateTime fromDate, DateTime toDate, string auditStatus, int eLevel, string empName = "")
        {
            var dep = db.ei_department.Single(d => d.id == depId);

            //保存到cookie
            var sm = new ALSearchParam()
            {
                depId = depId.ToString(),
                depName = MyUtils.EncodeToUTF8(GetDepLongNameByNum(dep.FNumber)),
                fromDate = fromDate.ToString("yyyy-MM-dd"),
                toDate = toDate.ToString("yyyy-MM-dd"),
                auditStatus = MyUtils.EncodeToUTF8(auditStatus),
                empLeve = eLevel,
                empName = MyUtils.EncodeToUTF8(empName)
            };            
            var cookie = new HttpCookie("alReport");
            cookie.Values.Add("sm", JsonConvert.SerializeObject(sm));
            cookie.Expires = DateTime.Now.AddDays(7);
            Response.AppendCookie(cookie);

            toDate = toDate.AddDays(1);
            var myData = (from v in db.vw_askLeaveReport
                          where v.dep_no.StartsWith(dep.FNumber)
                          && v.from_date <= toDate && v.to_date >= fromDate
                          && (auditStatus == "所有" || v.status == auditStatus)
                          && (eLevel == -1 || v.emp_level == eLevel)
                          && v.applier_name.Contains(empName)
                          orderby v.from_date
                          select v).ToList();

            WriteEventLog("请假报表", "开始查询：" + JsonConvert.SerializeObject(sm));
            return myData;
        }

        [SessionTimeOutFilter]
        public ActionResult CheckALDatas(int depId, DateTime fromDate, DateTime toDate, string auditStatus, int eLevel, string empName = "")
        {
            empName = empName.Trim();
            var list = GetALDatas(depId, fromDate, toDate, auditStatus, eLevel, empName);
            var dep = db.ei_department.Single(d => d.id == depId);            

            var sm = new ALSearchParam()
            {
                depId = depId.ToString(),
                depName = GetDepLongNameByNum(dep.FNumber),
                fromDate = fromDate.ToString("yyyy-MM-dd"),
                toDate = toDate.ToString("yyyy-MM-dd"),
                auditStatus = auditStatus,
                empLeve = eLevel,
                empName = empName
            };            

            ViewData["sm"] = sm;
            ViewData["list"] = list;
            ViewData["empLevels"] = db.ei_empLevel.OrderBy(m => m.level_no).ToList();
            
            return View("ALReport");
        }

        [SessionTimeOutFilter]
        public void BeginExportALExcel(int depId, DateTime fromDate, DateTime toDate, string auditStatus, int eLevel, string empName="")
        {
            var myData = GetALDatas(depId, fromDate, toDate, auditStatus, eLevel, empName);
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
                cells.Add(rowIndex, ++colIndex, GetUserNameAndCardByCardNum(d.agent_man));
                cells.Add(rowIndex, ++colIndex, GetUserNameAndCardByCardNum(d.inform_man));
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

            WriteEventLog("请假报表:"+sysNo, string.Format("修改请假日期：{0}~{1}--->{2}~{3};{4}天{5}小时--->{6}天{7}小时",
                ((DateTime)al.from_date).ToString("yyyy-MM-dd HH:mm"), ((DateTime)al.to_date).ToString("yyyy-MM-dd HH:mm"),
                fromDateDt.ToString("yyyy-MM-dd HH:mm"), toDateDt.ToString("yyyy-MM-dd HH:mm"), al.work_days, al.work_hours,
                days, hours));

            try {
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

    }
}
