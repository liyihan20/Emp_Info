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
                          && u.short_dep_name != null
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

        //选择部门
        public JsonResult GetDepartmentTreeForSel()
        {
            var list = new List<Department>();
            foreach (var d in db.ei_department.Where(d => d.FNumber.Length == 1).ToList()) {
                list.Add(GetDepartment(d.FNumber));
            }
            return Json(list);
        }

        private Department GetDepartment(string rootNumber)
        {
            var rootDep = db.ei_department.Single(e => e.FNumber == rootNumber);            
            Department dep = new Department();            
            dep.text = rootDep.FName;
            dep.tags = new string[] { rootDep.FNumber,rootDep.id.ToString() };
            dep.selectable = true;

            dep.nodes = new List<Department>();            
            foreach (var child in db.ei_department
                .Where(e => e.FParent == rootNumber && (e.FIsDeleted == null || e.FIsDeleted == false) && (e.FIsForbit==null || e.FIsForbit==false))
                .OrderBy(e => e.FNumber).ToList()) {
                dep.nodes.Add(GetDepartment(child.FNumber)); //递归获取子节点
            }
            if (dep.nodes.Count() == 0) {
                dep.nodes = null; //没有子节点
            }
            return dep;
        }
    }
}
