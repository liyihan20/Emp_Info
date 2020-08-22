using EmpInfo.Filter;
using EmpInfo.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Newtonsoft.Json;
using EmpInfo.Services;
using EmpInfo.Util;

namespace EmpInfo.Controllers
{
    public class OthersController : BaseController
    {
        #region 辅助、基干人员查询

        [AuthorityFilter]
        public ActionResult AssistantEmps()
        {
            return View();
        }

        public JsonResult GetAssistantEmps(string search = "", int offset = 0, int limit = 10)
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

            return Json(new { total = total, rows = result.ToList() }, JsonRequestBehavior.AllowGet);
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

        #region 宿舍员工查询

        [AuthorityFilter]
        public ActionResult DormAndEmps()
        {
            
            return View();
        }

        public JsonResult GetDormInfo(string dormNumber)
        {
            try {
                var result = db.GetDormInfoByGuard(dormNumber).ToList();
                if (result.Count() == 0) {
                    return Json(new SimpleResultModel(false, "查询不到此宿舍的住宿信息"));
                }
                return Json(new SimpleResultModel(true, "", JsonConvert.SerializeObject(result)));
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel(false, "获取住宿信息失败，原因:" + ex.Message));
            }
        }

        public JsonResult GetDormEmp(string empInfo)
        {
            try {
                var result = db.GetDormEmpByGuard(empInfo).ToList();
                if (result.Count() == 0) {
                    return Json(new SimpleResultModel(false, "查询不到此员工的住宿信息"));
                }
                return Json(new SimpleResultModel(true, "", JsonConvert.SerializeObject(result)));
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel(false, "获取住宿信息失败，原因:" + ex.Message));
            }
        }

        public JsonResult GetDormEmptyRoom()
        {
            try {
                var result = db.vw_dormEmptyRoom.ToList();                
                result.Sort(new DormComparer());

                //增加工业城中各个房型的统计                
                var summary = new List<vw_dormEmptyRoom>();
                summary.Add(new vw_dormEmptyRoom()
                {
                    area_name="工业城汇总",
                    room_num = result.Where(r => !r.area_name.Contains("红草")).Sum(r => r.room_num),
                    living_room_num = result.Where(r => !r.area_name.Contains("红草")).Sum(r => r.living_room_num)
                });
                summary.Add(new vw_dormEmptyRoom()
                {
                    area_name = "红草汇总",
                    room_num = result.Where(r => r.area_name.Contains("红草")).Sum(r => r.room_num),
                    living_room_num = result.Where(r => r.area_name.Contains("红草")).Sum(r => r.living_room_num)
                });

                var dormTypes = result.Where(r => !r.area_name.Contains("红草")).Select(r => r.dorm_type).Distinct().ToList();
                foreach (var dormType in dormTypes) {
                    summary.Add(new vw_dormEmptyRoom()
                    {
                        area_name = "工业城",
                        dorm_type = dormType,
                        room_num = result.Where(r => !r.area_name.Contains("红草") && r.dorm_type == dormType).Sum(r => r.room_num),
                        living_room_num = result.Where(r => !r.area_name.Contains("红草") && r.dorm_type == dormType).Sum(r => r.living_room_num)
                    });
                }

                var result2 = summary.Union(result);
                return Json(new SimpleResultModel(true, "", JsonConvert.SerializeObject(result2)));
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel(false, "获取住宿信息失败，原因:" + ex.Message));
            }
        }


        //对宿舍统计信息进行排序
        private class DormComparer : IComparer<vw_dormEmptyRoom>
        {
            public int Compare(vw_dormEmptyRoom x, vw_dormEmptyRoom y)
            {
                var nums = new Dictionary<string, int>();
                nums.Add("一", 1);
                nums.Add("二", 2);
                nums.Add("三", 3);
                nums.Add("四", 4);
                nums.Add("五", 5);
                nums.Add("六", 6);
                nums.Add("七", 7);
                nums.Add("八", 8);
                nums.Add("红草一", -3);
                nums.Add("红草二", -2);
                nums.Add("红草三", -1);

                decimal xn = 0, yn = 0;
                if (!x.area_name.Equals(y.area_name)) {
                    //不同宿舍区按照字典的value升序排序
                    foreach (var num in nums) {
                        if (x.area_name.StartsWith(num.Key)) {
                            xn = num.Value;
                        }
                        if (y.area_name.StartsWith(num.Key)) {
                            yn = num.Value;
                        }
                    }
                }
                else {
                    //同一宿舍区的按照管理费排序，管理费越高表示面积越大
                    xn = x.manage_cost;
                    yn = y.manage_cost;
                }

                return xn.CompareTo(yn);
            }
        }
        
        #endregion

        #region 宿舍收支统计

        [AuthorityFilter]
        public ActionResult DormFeeReport()
        {
            ViewData["yearMonth"] = db.GetDormChargeMonth().ToList().Where(d => d.CompareTo("202005") >= 0).ToList();
            return View();
        }

        public JsonResult GetDormFeeReport(string yearMonth)
        {
            DateTime fromDate = DateTime.Parse(yearMonth + "-01");
            DateTime toDate = fromDate.AddMonths(1);
            List<DormReportModel> result;
            try {
                result = db.Database.SqlQuery<DormReportModel>("exec [192.168.100.205].[LogisticsDB].[dbo].[getChargeTypeReport] @fd = {0:yyyy-MM-dd},@td = {1:yyyy-MM-dd}", fromDate, toDate).ToList();
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel(ex));
            }

            //加入工程支出的
            decimal deSum = db.ei_DEApplyEntry.Where(d => d.clear_date >= fromDate && d.clear_date < toDate).Sum(d => d.total_with_tax) ?? 0;
            result.Add(new DormReportModel() { charge_type = "工程支出", type_sum = deSum });

            //从工资系统查询的后勤工资、宿舍房租和水电等
            result.Add(new DormReportModel() { charge_type = "后勤工资", type_sum = db.GetLodDepSalarySum(yearMonth.Replace("-", "")).ToList().Where(g => g.charge_type == "工资").Sum(g => g.type_sum) });
            result.Add(new DormReportModel() { charge_type = "工资代扣", type_sum = db.GetLodDepSalarySum(yearMonth.Replace("-", "")).ToList().Where(g => g.charge_type != "工资").Sum(g => g.type_sum) });

            var k3Datas = db.Database.SqlQuery<k3ReportModel>("select [物料名称],[金额] from v_erp_po where [日期] >= '" + fromDate.ToString("yyyy-MM-dd") + "' and [日期] < '" + toDate.ToString("yyyy-MM-dd") + "'").ToList();
            //加入辅料支出和设备类支出,奇怪的使用参数传参的形式总是报日期转化错误，只能用拼接的方式
            result.Add(new DormReportModel() { charge_type = "辅料类", type_sum = k3Datas.Where(k => k.物料名称 != "设备维修备件").Sum(k => k.金额) ?? 0m });
            result.Add(new DormReportModel() { charge_type = "设备类", type_sum = k3Datas.Where(k => k.物料名称 == "设备维修备件").Sum(k => k.金额) ?? 0m });

            return Json(new SimpleResultModel(true, "", JsonConvert.SerializeObject(result)));
        }

        private class k3ReportModel
        {
            public string 物料名称 { get; set; }
            public decimal? 金额 { get; set; }
        }

        public ActionResult GetDormFeeReportDetail(string yearMonth, string chargeType)
        {
            DateTime fromDate = DateTime.Parse(yearMonth + "-01");
            DateTime toDate = fromDate.AddMonths(1);

            string title = string.Format("{0}({1:yyyy-MM-dd} ~ {2:yyyy-MM-dd})", chargeType, fromDate, toDate.AddDays(-1));

            string sqltext = "";
            ConnectionModel cm = null;
            if ("工程支出".Equals(chargeType)) {
                sqltext = @"select 
                    catalog as [类别],subject as [项目],name as [名称],total_with_tax as [金额],
                    summary as [摘要],convert(varchar(10),clear_date,23) as [结算日期]
                    from ei_DEApplyEntry ";
                sqltext += string.Format("where clear_date >= '{0:yyyy-MM-dd}' and clear_date < '{1:yyyy-MM-dd}'", fromDate, toDate);
            }
            else if (new string[] { "辅料类", "设备类" }.Contains(chargeType)) {
                sqltext = @"select
                        convert(varchar(20),[日期],23) as [日期],[采购单号],[供应商],[物料名称],
                        [物料型号],[辅助属性],[采购数量],[含税单价],[金额],[摘要]
                        from v_erp_po ";
                sqltext += string.Format("where [日期] >= '{0:yyyy-MM-dd}' and [日期] < '{1:yyyy-MM-dd}' and [物料名称] {3} '{2}' order by [日期]", fromDate, toDate, "设备维修备件", chargeType.Equals("设备类") ? "=" : "<>");
            }
            else if ("工资代扣".Equals(chargeType)) {
                sqltext = @"declare @tb table(charge_type nvarchar(100), type_sum decimal(12,2))
                    insert into @tb(charge_type,type_sum)
                    exec [192.168.100.214].[truly_gz].dbo.tpro_getgzcost " + yearMonth.Replace("-", "") + @"
                    select charge_type as '类别',isnull(type_sum,0) as '金额' from @tb where charge_type <> '工资'";
            }
            else {
                cm = new ConnectionModel()
                {
                    serverName = "192.168.100.205",
                    dbName = "LogisticsDB",
                    dbLoginName = "Logistics",
                    dbPassword = "Logistics123456"
                };
                sqltext = string.Format("exec getChargeTypeReportDetail @fd = '{0:yyyy-MM-dd}',@td = '{1:yyyy-MM-dd}',@type = '{2}'", fromDate, toDate, chargeType);
            }

            //跳转到基础输出表格
            TempData["title"] = title;
            TempData["sqlText"] = sqltext;
            TempData["cm"] = cm;
            return RedirectToAction("BasicTable", "BI", null);

        }

        #endregion

        #region 厂区表格展示

        public ActionResult DS(int isEmpty = 0, string place = "", string depName = "",string depCharger="")
        {
            var result = from b in db.ei_bus_place
                         join e in db.ei_bus_place_detail on b.id equals e.place_id
                         orderby b.sort_no, e.floor_sort_no
                         select new BusPlaces()
                         {
                             place_id = b.id,
                             sort_no = b.sort_no,
                             place = b.place,
                             area_size = b.area_size,
                             floor = e.floor,
                             dep_name = e.dep_name,
                             dep_size = e.dep_size,
                             clear_level = e.clear_level,
                             dep_plan = e.dep_plan,
                             usage = e.usage,
                             pic_name = e.pic_name,
                             detail_id=e.id,
                             dep_charger=e.dep_charger,
                             is_empty=e.is_empty,
                             all_charger = e.all_charger,
                             produce_charger = e.produce_charger
                         };

            if (isEmpty == 1) {
                result = result.Where(r => r.is_empty);
            }

            if (!string.IsNullOrEmpty(place)) {
                result = result.Where(r => r.place == place);
            }

            if (!string.IsNullOrEmpty(depName)) {
                result = result.Where(r => r.dep_name.Contains(depName));
            }

            if (!string.IsNullOrEmpty(depCharger)) {
                result = result.Where(r => r.dep_charger == depCharger);
            }

            DSModel dm = new DSModel();
            dm.isEmpty = isEmpty;
            dm.place = place;
            dm.depName = depName;
            dm.depCharger = depCharger;
            dm.ps = result.ToList();
            dm.places = db.ei_bus_place.OrderBy(b => b.sort_no).Select(b => b.place).ToList();
            dm.chargers = db.ei_bus_place_detail.Where(b => b.dep_charger != null && b.dep_charger != "").Select(b => b.dep_charger).Distinct().ToList();

            ViewData["dm"] = dm;
            return View();
        }

        [AuthorityFilter]
        public ActionResult DSIndex()
        {
            return View();
        }        

        public JsonResult GetDSes()
        {
            var result = (from d in db.ei_bus_place
                          orderby d.sort_no
                          select new
                          {
                              d.id,
                              d.place,
                              d.sort_no,
                              d.area_size
                          }).ToList();
            return Json(result, JsonRequestBehavior.AllowGet);
        }

        public JsonResult GetDSDetail(int place_id)
        {
            var result = (from d in db.ei_bus_place_detail
                          where d.place_id == place_id
                          orderby d.floor_sort_no
                          select new
                          {
                              d.id,
                              d.clear_level,
                              d.dep_name,
                              d.dep_plan,
                              d.dep_size,
                              d.floor,
                              d.place_id,
                              d.usage,
                              d.pic_name,
                              d.dep_charger,
                              is_empty = d.is_empty ? "是" : "",
                              d.all_charger,
                              d.produce_charger,
                              d.floor_sort_no
                          }).ToList();
            return Json(result, JsonRequestBehavior.AllowGet);
        }

        public JsonResult SaveDs(string obj)
        {
            try {
                ei_bus_place bp = JsonConvert.DeserializeObject<ei_bus_place>(obj);
                if (bp.id == 0) {
                    if (db.ei_bus_place.Where(b => b.place == bp.place).Count() > 0) {
                        return Json(new SimpleResultModel(false, "存在重复的地点，不能保存：" + bp.place));
                    }                    
                    db.ei_bus_place.Add(bp);
                }
                else {
                    if (db.ei_bus_place.Where(b => b.place == bp.place && b.id != bp.id).Count() > 0) {
                        return Json(new SimpleResultModel(false, "存在重复的地点，不能保存：" + bp.place));
                    }
                    var exiest = db.ei_bus_place.Where(b => b.id == bp.id).FirstOrDefault();
                    MyUtils.CopyPropertyValue(bp, exiest);
                }
                db.SaveChanges();
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel(ex));
            }
            return Json(new SimpleResultModel(true));
        }

        public JsonResult RemoveDs(int id)
        {
            try {
                if (db.ei_bus_place_detail.Where(e => e.place_id == id).Count() > 0) {
                    return Json(new SimpleResultModel(false, "此地点存在关联的楼层，不能删除；如确认需删除，请先删除对应的楼层后再操作。"));
                }
                db.ei_bus_place.Remove(db.ei_bus_place.Where(e => e.id == id).FirstOrDefault());
                db.SaveChanges();
                return Json(new SimpleResultModel(true));
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel(ex));
            }
        }

        public JsonResult SaveDsDetail(string obj)
        {
            try {
                ei_bus_place_detail bpd = JsonConvert.DeserializeObject<ei_bus_place_detail>(obj);
                if (bpd.id == 0) {
                    bpd.pic_name = "";
                    db.ei_bus_place_detail.Add(bpd);
                }
                else {
                    var existed = db.ei_bus_place_detail.Where(e => e.id == bpd.id).FirstOrDefault();
                    MyUtils.CopyPropertyValue(bpd, existed);
                }
                db.SaveChanges();
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel(ex));
            }

            return Json(new SimpleResultModel(true));
        }

        public JsonResult RemoveDsDetail(int id)
        {
            try {
                db.ei_bus_place_detail.Remove(db.ei_bus_place_detail.Where(e => e.id == id).FirstOrDefault());
                db.SaveChanges();
                return Json(new SimpleResultModel(true));
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel(ex));
            }
        }

        public ActionResult CheckDSPic(int id)
        {
            var detail = db.ei_bus_place_detail.Where(d => d.id == id).FirstOrDefault();
            if (detail == null) {
                return Content("楼层不存在或已删除");
            }
            else {
                if (string.IsNullOrEmpty(detail.pic_name)) {
                    return Content("平面图不存在");
                }
                else {
                    return File(detail.pic, @"image/bmp");
                }
            }
        }

        public JsonResult RemoveDSPic(int id)
        {
            var detail = db.ei_bus_place_detail.Where(d => d.id == id).FirstOrDefault();
            if (detail == null) {
                return Json(new SimpleResultModel(false, "楼层不存在或已删除"));
            }
            detail.pic = null;
            detail.pic_name = "";
            db.SaveChanges();

            return Json(new SimpleResultModel(true));
        }

        #endregion

        #region 经营报表统计

        public ActionResult ManagementReport()
        {
            if (db.ei_flowAuthority.Where(f => f.bill_type == "ManageR" && f.relate_value == userInfo.cardNo && f.relate_type == "查看报表").Count() < 1) {
                return View("Warn");
            }
            ViewData["depList"] = db.Database.SqlQuery<string>("select yname from [192.168.100.205].truly_data.dbo.jy_dept where close_flag=1 order by yname").ToList();

            return View();
        }

        public JsonResult GetManagementReport(string depName, string yearMonth)
        {
            yearMonth = yearMonth.Replace("-", "");
            string sqlText = string.Format("exec [192.168.100.205].truly_data.dbo.tpro_jy_data_yybb @dept_name = {0},@month_no = {1}", depName, yearMonth);
            try {
                var result = new BIBaseSv().GetTableResult(sqlText);

                return Json(new { suc = true, columns = result.columns, rows = result.rows });
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel(ex));
            }            

        }

        #endregion

    }
}
