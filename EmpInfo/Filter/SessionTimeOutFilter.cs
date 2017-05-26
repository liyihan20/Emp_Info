using EmpInfo.Models;
using EmpInfo.Util;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace EmpInfo.Filter
{

    public class SessionTimeOutFilterAttribute : ActionFilterAttribute
    {

        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var ctx = filterContext.HttpContext;
            if (ctx.Session != null)
            {
                var cookie = ctx.Request.Cookies[ConfigurationManager.AppSettings["cookieName"]];
                if (cookie != null)
                {
                    var id = cookie.Values.Get("userid");
                    var code = cookie.Values.Get("code");
                    if (code.Equals(MyUtils.getMD5(id)))
                    {
                        base.OnActionExecuting(filterContext);
                        return;
                    }
                }
            }
            filterContext.Result = new RedirectResult("~/Account/Login");
            //ctx.Response.Redirect("~/Account/Login");--虽可正常运行，但在调试模式下回出错，因为还是会在Action里面继续执行。
        }
                
    }

    public class SessionTimeOutJsonFilterAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var ctx = filterContext.HttpContext;
            if (ctx.Session != null) {
                var cookie = ctx.Request.Cookies[ConfigurationManager.AppSettings["cookieName"]];
                if (cookie != null) {
                    var id = cookie.Values.Get("userid");
                    var code = cookie.Values.Get("code");
                    if (code.Equals(MyUtils.getMD5(id))) {
                        base.OnActionExecuting(filterContext);
                        return;
                    }
                }
            }

            filterContext.Result = new JsonResult()
            {
                Data = new SimpleResultModel() { suc = false, msg = "操作失败！原因：会话已过期，请重新登陆系统" }
            };
        }
    }

    //权限过滤器，验证用户是否有访问该控制器和方法的权限。
    public class AuthorityFilterAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            string actionName = filterContext.ActionDescriptor.ActionName;
            string controlerName = filterContext.ActionDescriptor.ControllerDescriptor.ControllerName;

            HttpContextBase ctx = filterContext.HttpContext;
            var cookie = ctx.Request.Cookies[ConfigurationManager.AppSettings["cookieName"]];
            var id = cookie.Values.Get("userid");
            if (MyUtils.hasGotPower(int.Parse(id), controlerName, actionName))
            {
                base.OnActionExecuting(filterContext);
                return;
            }
            else
            {
                filterContext.Result = new RedirectResult("~/Account/NoPowerToVisit?controlerName=" + controlerName + "&actionName=" + actionName);
                //ctx.Response.Redirect("~/Home/NoPowerToVisit?controlerName=" + controlerName + "&actionName=" + actionName);
            }

        }
    }
}