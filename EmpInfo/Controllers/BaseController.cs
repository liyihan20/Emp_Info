using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using EmpInfo.Models;
using EmpInfo.Util;
using System.Configuration;

namespace EmpInfo.Controllers
{
    public class BaseController : Controller
    {
        private ICAuditEntities _db = null; //本系统数据库
        private CanteenEntities _canteenDb = null; //食堂数据库
        private UserInfo _userInfo = null;  //暂存用户简要信息
        private UserInfoDetail _userInfoDetail = null;  //暂存用户详细信息
        private DinningCardStatusModel _dinningCarStatusModel = null;  //饭卡状态信息

        protected ICAuditEntities db
        {
            get
            {
                if (_db == null)
                {
                    _db = new ICAuditEntities();
                }
                return _db;
            }
        }

        protected CanteenEntities canteenDb
        {
            get
            {
                if (_canteenDb == null)
                {
                    _canteenDb = new CanteenEntities();
                }
                return _canteenDb;
            }
        }

        protected UserInfo userInfo
        {
            get
            {
                _userInfo = (UserInfo)Session["userInfo"];
                if (_userInfo == null)
                {
                    var cookie = Request.Cookies[ConfigurationManager.AppSettings["cookieName"]];
                    if (cookie != null)
                    {
                        _userInfo = new UserInfo();
                        _userInfo.id = Int32.Parse(cookie.Values.Get("userid"));
                        _userInfo.name = MyUtils.DecodeToUTF8(cookie.Values.Get("username"));
                        _userInfo.cardNo = cookie.Values.Get("cardno");                        
                    }
                    Session["userInfo"] = _userInfo;                    
                }

                return _userInfo;
            }
        }

        protected UserInfoDetail userInfoDetail
        {
            get
            {
                _userInfoDetail = (UserInfoDetail)Session["userInfoDetail"];
                if (_userInfoDetail == null)
                {
                    if (userInfo != null)
                    {
                        _userInfoDetail = new UserInfoDetail();
                        var user = db.ei_users.Single(u => u.id == userInfo.id);
                        _userInfoDetail.email = user.email;
                        _userInfoDetail.sex = user.sex;
                        _userInfoDetail.shortPhone = user.short_phone;
                        _userInfoDetail.phone = user.phone;
                        _userInfoDetail.idNumber = user.id_number;
                        _userInfoDetail.salaryNo = user.salary_no;
                    }
                    Session["userInfoDetail"] = _userInfoDetail;
                }
                return _userInfoDetail;
            }
        }

        //饭卡基本信息
        protected DinningCardStatusModel dinningCarStatusModel
        {
            get
            {
                _dinningCarStatusModel = (DinningCardStatusModel)Session["dinningCarStatus"];
                if (_dinningCarStatusModel == null) {
                    var cardStatus = canteenDb.ljq20160323_002(userInfo.cardNo).ToList();
                    _dinningCarStatusModel = new DinningCardStatusModel();
                    _dinningCarStatusModel.username = userInfo.name;
                    _dinningCarStatusModel.cardNo = userInfo.cardNo;
                    if (cardStatus.Count() < 1) { 
                        _dinningCarStatusModel.status = "不存在";
                        _dinningCarStatusModel.remainingSum = 0;
                        _dinningCarStatusModel.lastConsumeTime = "无";
                    }
                    else {
                        var st = cardStatus.First();
                        _dinningCarStatusModel.status = st.FStatus;
                        _dinningCarStatusModel.lastConsumeTime = st.DatLastConsumeTime == null ? "无" : ((DateTime)st.DatLastConsumeTime).ToString("yyyy-MM-dd HH:mm:ss");
                        _dinningCarStatusModel.remainingSum = st.MonBalance;
                    }
                    Session["dinningCarStatus"] = _dinningCarStatusModel;
                }
                return _dinningCarStatusModel;
            }
        }

        //清空饭卡基本信息Session
        protected void CleardinningCarStatus()
        {
            if (Session["dinningCarStatus"] != null) {
                Session["dinningCarStatus"] = null;
            }
        }

        //清空用户详细，用于刷新用户信息
        protected void ClearUserInfoDetail()
        {
            if (Session["userInfo"] != null)
            {
                Session["userInfo"] = null;
            }
            if (Session["userInfoDetail"] != null)
            {
                Session["userInfoDetail"] = null;
            }
        }

        protected string GetUserIP()
        {
            string loginip = "";  
            ////Request.ServerVariables[""]--获取服务变量集合   
            //if (Request.ServerVariables["REMOTE_ADDR"] != null) //判断发出请求的远程主机的ip地址是否为空  
            //{  
            //    //获取发出请求的远程主机的Ip地址  
            //    loginip = Request.ServerVariables["REMOTE_ADDR"].ToString();  
            //}  
            ////判断登记用户是否使用设置代理  
            //else if (Request.ServerVariables["HTTP_VIA"] != null)  
            //{  
            //    if (Request.ServerVariables["HTTP_X_FORWARDED_FOR"] != null)  
            //    {  
            //        //获取代理的服务器Ip地址  
            //        loginip = Request.ServerVariables["HTTP_X_FORWARDED_FOR"].ToString();  
            //    }  
            //    else  
            //    {  
            //        //获取客户端IP  
            //        loginip = Request.UserHostAddress;  
            //    }  
            //}  
            //else  
            //{  
            //    //获取客户端IP  
            //    loginip = Request.UserHostAddress;  
            //}
            loginip = Request.UserHostAddress; 
            return loginip;  
        }

        //记录操作日志
        protected void WriteEventLog(string model, string doWhat, int isNomal = 0)
        {
            var log = new ei_event_log();
            log.ip = GetUserIP();
            log.do_what = doWhat;
            log.is_normal = isNomal;
            log.model = model;
            log.op_date = DateTime.Now;
            log.user_id = userInfo.id;
            log.user_name = userInfo.name;
            db.ei_event_log.Add(log);
            db.SaveChanges();
        }

        //未登陆时的记录日志方法
        protected void WriteEventLogWithoutLogin(string cardNo, string msg, int isNomal = 0)
        {
            var log = new ei_event_log();
            log.ip = GetUserIP();
            log.user_name = cardNo;
            log.do_what = msg;
            log.is_normal = isNomal;
            log.op_date = DateTime.Now;
            log.model = "登陆注册模块";
            db.ei_event_log.Add(log);
            db.SaveChanges();
        }

        //设置cookie
        protected void setcookie(ei_users user,int days)
        {
            var cookie = new HttpCookie(ConfigurationManager.AppSettings["cookieName"]);
            cookie.Expires = DateTime.Now.AddDays(days);
            cookie.Values.Add("userid", user.id.ToString());
            cookie.Values.Add("cardno", user.card_number);
            cookie.Values.Add("code", MyUtils.getMD5(user.id.ToString()));
            cookie.Values.Add("username", MyUtils.EncodeToUTF8(user.name));//用于记录日志
            Response.AppendCookie(cookie);
        }

        //获取流水号
        protected string GetNextSysNum(string billType)
        {
            string result = billType + DateTime.Now.ToString("yyMMdd");
            var maxNumRecords = db.all_maxNum.Where(a => a.bill_type == billType && a.date_str == result);
            if (maxNumRecords.Count() > 0) {
                var maxNumRecord = maxNumRecords.First();
                result += string.Format("{0:D3}", maxNumRecord.max_num);
                maxNumRecord.max_num = maxNumRecord.max_num + 1;
            }
            else {
                var maxNumRecord = new all_maxNum();
                maxNumRecord.bill_type = billType;
                maxNumRecord.date_str = result;
                maxNumRecord.max_num = 2;
                db.all_maxNum.Add(maxNumRecord);

                result += "001";
            }

            db.SaveChanges();

            return result;
        }

        //是否拥有某权限
        protected bool HasGotPower(string powerName)
        {
            var powers = (from g in db.ei_groups
                          from a in g.ei_groupAuthority
                          from gu in g.ei_groupUser
                          where gu.user_id == userInfo.id
                          && a.ei_authority.en_name == powerName
                          select a).ToList();
            if (powers.Count() > 0) {
                return true;
            }
            return false;
        }

    }


}
