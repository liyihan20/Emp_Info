using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using EmpInfo.Util;
using EmpInfo.Models;
using EmpInfo.Filter;

namespace EmpInfo.Controllers
{
    public class ChartController : BaseController
    {

        [SessionTimeOutFilter]
        [AuthorityFilter]
        public ActionResult ChartIndex()
        {
            var auts = (from a in db.ei_authority
                        from g in db.ei_groups
                        from gu in g.ei_groupUser
                        from ga in g.ei_groupAuthority
                        where ga.authority_id == a.id
                        && gu.user_id == userInfo.id
                        && a.number > 4 && a.number < 5
                        select a.en_name).Distinct().ToArray();
            string autStr = string.Join(",", auts);
            ViewData["autStr"] = autStr;
            return View();
        }

        #region 食堂数据分析

        [SessionTimeOutFilter]
        public ActionResult ResChart()
        {
            ViewData["Today"] = MyUtils.GetWeekDay(DateTime.Now.DayOfWeek) + "  " + DateTime.Now.ToString("yyyy-MM-dd");
            WriteEventLog("食堂营业额图表", "打开界面");
            return View();
        }

        //获取食堂人数数据
        public JsonResult GetResDatas()
        {
            try {
                var result = canteenDb.laijq20161105_001();
                List<ResConsumeData> datas = new List<ResConsumeData>();

                foreach (var res in result) {
                    datas.Add(new ResConsumeData()
                    {
                        name = res.FFloor,
                        value = res.SumMonConsume
                    });
                }

                var result2 = canteenDb.laijq20161105_002().First();
                return Json(new { suc = true, all = result2.Total, segement = result2.FType, list = datas });
            }
            catch {
                return Json(new { suc = false, msg = "数据获取失败，请稍后再试或联系信息中心" });
            }

        }

        #endregion

    }
}
