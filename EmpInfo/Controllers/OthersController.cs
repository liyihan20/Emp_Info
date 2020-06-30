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
            ViewData["yearMonth"] = db.GetDormChargeMonth().ToList().Where(d => d.CompareTo("202005") >= 0).ToList();
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
            return Json(new SimpleResultModel(true, "", JsonConvert.SerializeObject(result)));
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
        
        #region 厂区表格展示
        
        public ActionResult DS(int isEmpty = 0, string place = "", string depName = "")
        {
            var result = from b in db.ei_bus_place
                         join e in db.ei_bus_place_detail on b.id equals e.place_id
                         orderby b.sort_no, e.floor
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
                             usage = e.usage
                         };

            if (isEmpty == 1) {
                result = result.Where(r => r.dep_plan.Contains("闲置") || r.dep_name.Contains("闲置"));
            }

            if (!string.IsNullOrEmpty(place)) {
                result = result.Where(r => r.place == place);
            }

            if (!string.IsNullOrEmpty(depName)) {
                result = result.Where(r => r.dep_name.Contains(depName));
            }

            ViewData["isEmpty"] = isEmpty;
            ViewData["place"] = place;
            ViewData["depName"] = depName;
            ViewData["ps"] = result.ToList();
            ViewData["places"] = db.ei_bus_place.Select(b => b.place).Distinct().ToList();
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
                          orderby d.floor
                          select new
                          {
                              d.id,
                              d.clear_level,
                              d.dep_name,
                              d.dep_plan,
                              d.dep_size,
                              d.floor,
                              d.place_id,
                              d.usage
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

        #endregion
    }
}
