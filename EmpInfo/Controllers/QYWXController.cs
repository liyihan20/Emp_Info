using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using EmpInfo.QywxWebSrv;
using EmpInfo.Services;
using EmpInfo.Util;
using System.IO;
using System.Text;
using System.Xml;
using EmpInfo.Models;
using EmpInfo.Interfaces;

namespace EmpInfo.Controllers
{
    /// <summary>
    /// 企业微信相关开发
    /// </summary>
    public class QYWXController : BaseController
    {
        private const string APPID = "wwd136c62daa97a189"; //企业ID
        private const string SECRET = "wZRxdsuqeFAqJDG7VLaCTkImfsuce0qwyLO3ksBUkMY"; //应用secret
        private const string AGENTID = "1000007"; //应用id
        private const string LOGIN_URL = "http://emp.truly.com.cn/emp/QYWX/Login";

        private const string TOKEN = "daGjLiUJ65Os7E39qVRm8r2tYyCp";
        private const string ENCODINGAESKEY = "v6cJQ1YJOke9Z6MnLHHi0kS5MO1MeJvH1FkcAoia43C";

        #region 登录、免密进入系统

        /// <summary>
        /// 利用该企业微信免密进入的链接
        /// </summary>
        /// <param name="code">由企业微信官方带过来的code，用以获取userid</param>
        /// <param name="returnUrl">需要跳转到的本地链接</param>
        /// <param name="state"></param>
        /// <returns></returns>
        [AllowAnonymous]
        public ActionResult Login(string code, string returnUrl = "",string state="")
        {
            if (string.IsNullOrEmpty(state)) {
                Session["fromwx"] = true; //没有state的就是从手机端进入
            }
            else {
                if (Session["state"] == null || !state.Equals((string)Session["state"])) {
                    ViewBag.tip = "state验证错误，登录失败";
                    return View("Error");
                }
                Session["fromwx"] = false; //从网页端进入
            }

            string userID;
            if (userInfo == null) {
                if(string.IsNullOrEmpty(code)){
                    ViewBag.tip="获取不到code，授权失败";
                    return View("Error");
                }

                //没有cookie的，通过code获取userid，也即是厂牌
                QywxApiSrvSoapClient wx = new QywxApiSrvSoapClient();
                try {
                    userID = wx.GetUserIdFromCode(SECRET, code);
                }
                catch (Exception ex) {
                    ViewBag.tip = ex.Message;
                    return View("Error");
                }

                if (!new HRDBSv().EmpIsNotQuit(userID)) {
                    ViewBag.tip = "不是在职员工，不能使用此应用";
                    return View("Error");
                }

            }
            else {
                userID = userInfo.cardNo;
            }
            var user = db.ei_users.Where(u => u.card_number == userID).FirstOrDefault();
            if (user == null) {
                return RedirectToAction("Login", "Account", new { });
            }
            
            user.last_login_date = DateTime.Now;
            setcookie(user, 1);
            
            WriteEventLog("企业微信", "进入应用：" + returnUrl);

            if (!string.IsNullOrEmpty(returnUrl)) {
                return Redirect(Uri.UnescapeDataString(returnUrl));
            }

            return RedirectToAction("Index", "Home", new { });
        }

        public string test()
        {
            QywxApiSrvSoapClient wx = new QywxApiSrvSoapClient();
            return wx.GetOAthLink("http://emp.truly.com.cn/emp/home/index?from=hello");
        }

        /// <summary>
        /// 网页端进入的链接
        /// </summary>
        /// <returns></returns>
        public ActionResult ComputerWebLoginUrl()
        {
            QywxApiSrvSoapClient wx = new QywxApiSrvSoapClient();
            string state = MyUtils.CreateValidateNumber(6);
            Session["state"] = state;
            string url = wx.GetWebLink(AGENTID, LOGIN_URL, state);
            return Redirect(url);
        }

        /// <summary>
        /// 企业微信二次验证
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public ActionResult SecondValify(string code)
        {
            if (string.IsNullOrEmpty(code)) {
                ViewBag.tip = "获取不到code，授权失败";
                return View("Error2");
            }
            string userID = "";
            QywxApiSrvSoapClient wx = new QywxApiSrvSoapClient();
            try {
                userID = wx.GetUserIdFromCode(SECRET, code);
            }
            catch (Exception ex) {
                ViewBag.tip = ex.Message;
                return View("Error2");
            }

            var vf = db.qywx_secondeValify.Where(q => q.card_number == userID).FirstOrDefault();
            if (vf == null) {
                vf = new qywx_secondeValify();
                vf.card_number = userID;
                vf.code = code;
            }
            else {
                if (vf.suc == true) {
                    ViewBag.tip = "你已通过验证，不需再次验证";
                    return View("Error2");
                }
                vf.code = code;
            }
            TempData["card_number"] = userID;

            return View();
        }

        #endregion

        #region js接口

        public ActionResult JsInterface(string actionType, string debug = "false")
        {            
            QywxApiSrvSoapClient wx = new QywxApiSrvSoapClient();

            QywxJsConfigParam p = new QywxJsConfigParam();
            p.timestamp = MyUtils.GetTimeStamp();
            p.appId = APPID;
            p.nonceStr = MyUtils.CreateValidateNumber(8);
            p.debug = debug;
            p.actionType = actionType;

            try {
                p.signature = wx.GetSignature(SECRET, p.nonceStr, p.timestamp, Request.Url.ToString());
            }
            catch (Exception ex) {
                ViewBag.tip = ex.Message;
                return View("Error");
            }
            ViewData["qywxConfigParam"] = p;

            return View();
        }

        public ActionResult HandleJsResult(string actionType, string result)
        {            
            try {
                switch (actionType) {
                    case "scanQRCode":                        
                        var resultArr = result.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);                        
                        SetBillByType(resultArr[0]);
                        var iJs = bill as IJsInterface;
                        if (iJs != null) {
                            var iJsResult = iJs.HandleJsInterface(resultArr[1], userInfo);
                            return RedirectToAction(iJsResult.actionName, iJsResult.controllerName, iJsResult.routetValues);
                        }
                        break;
                }
            }
            catch (Exception ex) {
                return Content("二维码内容:" + result + ";提示信息:" + ex.Message);
            }
            return Content(result);
        }

        #endregion
    }
}
