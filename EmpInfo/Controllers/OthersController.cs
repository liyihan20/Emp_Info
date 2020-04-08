using EmpInfo.Filter;
using EmpInfo.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Newtonsoft.Json;

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

    }
}
