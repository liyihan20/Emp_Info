using EmpInfo.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace EmpInfo.Controllers
{
    public class RestaurantBaseController : BaseController
    {
        private string _currentResNo = null;  //当前的食堂编码


        //当前所在的食堂
        protected string currentResNo
        {
            get
            {
                _currentResNo = (string)Session["currentResNo"];
                if (string.IsNullOrEmpty(_currentResNo)) {
                    var resLog = db.ei_resVisitLog.Where(r => r.user_no == userInfo.cardNo);
                    if (resLog.Count() > 0) {
                        _currentResNo = resLog.OrderByDescending(r => r.id).First().res_no;
                    }
                    Session["currentResNo"] = _currentResNo;
                }
                return _currentResNo;
            }
            set
            {
                _currentResNo = value;
                Session["currentResNo"] = _currentResNo;
                ei_resVisitLog log = new ei_resVisitLog()
                {
                    res_no = value,
                    user_no = userInfo.cardNo,
                    user_name = userInfo.name,
                    visit_date = DateTime.Now
                };
                db.ei_resVisitLog.Add(log);
                db.SaveChanges();
            }
        }

        //根据订餐时间获取当前食堂的餐别
        protected string GetTimeSegByOrderTime(DateTime dt){
            string result = "";
            var segs = (from i in db.dn_items
                       where i.comment.EndsWith("时间段")
                       && i.res_no == currentResNo
                       && i.value != "无"
                       select new
                       {
                           segValue = i.value,
                           segName = i.comment.Substring(0, 2)
                       }).ToList();
            foreach (var seg in segs) {
                DateTime time1, time2;
                time1 = DateTime.Parse(dt.ToString("yyyy-MM-dd") + " " + seg.segValue.Split('~')[0]);
                time2 = DateTime.Parse(dt.ToString("yyyy-MM-dd") + " " + seg.segValue.Split('~')[1]);
                if (dt >= time1 && dt <= time2) {
                    result = seg.segName;
                    break;
                }
            }
            return result;
        }

    }
}
