using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using EmpInfo.Models;
using EmpInfo.Util;
using EmpInfo.Filter;

namespace EmpInfo.Controllers
{
    
    public class ItemController : BaseController
    {
        [SessionTimeOutJsonFilter]
        public JsonResult GetUserBySelect()
        {
            var users = (from u in db.ei_users
                         select new SelectModel()
                         {
                             text = u.name + "[" + u.card_number + "]",
                             intValue = u.id
                         }).ToList();
            return Json(users);
        }

        [SessionTimeOutJsonFilter]
        public JsonResult GetAutBySelect()
        {
            var auts = (from a in db.ei_authority
                        select new SelectModel()
                        {
                            text = a.name,
                            intValue = a.id
                        }).ToList();
            return Json(auts);
        }

        //在职（有部门）的员工查询
        [SessionTimeOutJsonFilter]
        public JsonResult SearchWorkingEmp(string queryString)
        {
            var result = (from u in db.vw_ei_simple_users
                          where (u.card_number.Contains(queryString)
                          || u.name.Contains(queryString))
                          && (u.short_dep_name != null || u.card_number.StartsWith("GN"))
                          select new
                          {
                              cardNumber = u.card_number,
                              userName = u.name,
                              depName = u.short_dep_name
                          }).Take(100).ToList();
            return Json(new { suc = true, result = result });
        }

        //跳转到图片
        public ActionResult ShowPic(string picName)
        {
            ViewData["picName"] = picName;
            return View();
        }

        //选择部门,用于用户选择
        public JsonResult GetDepartmentTreeForSel()
        {
            var list = new List<Department>();
            var deps = db.ei_department.Where(d => (d.FIsDeleted == null || d.FIsDeleted == false) && (d.FIsForbit == null || d.FIsForbit == false)).ToList();
            foreach (var d in db.ei_department.Where(d => d.FNumber.Length == 1 && (d.FIsDeleted == null || d.FIsDeleted == false) && (d.FIsForbit == null || d.FIsForbit == false)).ToList()) {
                list.Add(GetDepartment(deps,d.FNumber));
            }
            return Json(list);
        }

        private Department GetDepartment(List<ei_department> deps, string rootNumber)
        {
            var rootDep = deps.Single(e => e.FNumber == rootNumber);            
            Department dep = new Department();            
            dep.text = rootDep.FName;
            dep.tags = new string[] { rootDep.FNumber,rootDep.id.ToString() };
            dep.selectable = true;
            if (rootDep.FIsForbit == true) {
                dep.color = "#d9534f"; //被禁用显示红色
            }

            dep.nodes = new List<Department>();
            foreach (var child in deps
                .Where(e => e.FParent == rootNumber)
                .OrderBy(e => e.FNumber).ToList()) {
                dep.nodes.Add(GetDepartment(deps,child.FNumber)); //递归获取子节点
            }
            if (dep.nodes.Count() == 0) {
                dep.nodes = null; //没有子节点
            }
            return dep;
        }

        //选择部门，用于部门管理员选择后做报表，只能看到自己的
        public JsonResult GetAdminDepartmentTreeForSel()
        {
            var list = new List<Department>();
            var deps = db.ei_department.Where(d => d.FIsDeleted == null || d.FIsDeleted == false).ToList();
            foreach (var d in db.ei_department.Where(d => d.FReporter.Contains(userInfo.cardNo) && (d.FIsDeleted == false || d.FIsDeleted == null)).Distinct().ToList()) {
                list.Add(GetDepartment(deps, d.FNumber));
            }
            return Json(list);
        }
        
        //获取k3的客户名称，通过客户编码
        public JsonResult GetK3CustomerName(string customerNumber)
        {
            var list = db.GetK3CustomerNameByNum(customerNumber).ToList();
            if (list.Count() < 1) {
                return Json(new SimpleResultModel() { suc = false, msg = "客户不存在，请确认客户编码是否正确" });
            }
            else {
                return Json(new SimpleResultModel() { suc = true, extra = list.First() });
            }

        }

    }
}
