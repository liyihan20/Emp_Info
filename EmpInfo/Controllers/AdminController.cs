using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using EmpInfo.Models;
using EmpInfo.Filter;
using EmpInfo.Util;
using System.Configuration;


namespace EmpInfo.Controllers
{
    
    public class AdminController : BaseController
    {
        int pageNumber = 30;

        [SessionTimeOutFilter]
        [AuthorityFilter]
        public ActionResult AdminIndex()
        {
            var auts = (from a in db.ei_authority
                        from g in db.ei_groups
                        from gu in g.ei_groupUser
                        from ga in g.ei_groupAuthority
                        where ga.authority_id == a.id
                        && gu.user_id == userInfo.id
                        && a.number > 1 && a.number < 2
                        select a.en_name).Distinct().ToArray();
            string autStr = string.Join(",", auts);
            ViewData["autStr"] = autStr;
            return View();
        }

        #region 用户管理
        [SessionTimeOutFilter]
        [AuthorityFilter]
        public ActionResult UserManagement()
        {
            var userCount = db.ei_users.Count();
            ViewData["userCount"] = userCount;
            return View();
        }

        [SessionTimeOutJsonFilter]
        public JsonResult GetUsers(string searchContent)
        {            
            int total;
            var users = (from v in db.vw_ei_simple_users
                         where v.card_number.Contains(searchContent)
                        || v.name.Contains(searchContent)
                        || v.salary_no.Contains(searchContent)
                        || v.id_number.Contains(searchContent)
                        //|| v.dep_name.Contains(searchContent)
                         select new UserListModel()
                         {
                             name = v.name,
                             sex = v.sex,
                             cardNo = v.card_number,
                             status = v.forbit_flag != true ? "正常" : "禁用",
                             depName = v.short_dep_name
                         }).ToList();
            Session["adminUserList"] = users;
            total = users.Count();
            if (total == 0) {
                return Json(new SimpleResultModel() { suc = false, msg = "没有符合条件的注册员工" });
            }
            return Json(new {suc = true, rows = users.Take(pageNumber), pages = Math.Ceiling((total * 1.0) / pageNumber) });
        }

        [SessionTimeOutJsonFilter]
        public JsonResult GetUsersPage(int page)
        {
            if(Session["adminUserList"]==null){
                return Json(new { suc = false });
            }
            List<UserListModel> users = (List<UserListModel>)Session["adminUserList"];
            

            return Json(new { suc = true, rows = users.Skip((page - 1) * pageNumber).Take(pageNumber).ToList() });
        }


        [SessionTimeOutFilter]
        public ActionResult CheckUserDetail(string cardNo)
        {
            var users = db.vw_ei_users.Where(u => u.card_number == cardNo).ToList();
            if (users.Count() != 1)
            {
                return View("error");
            }
            ViewData["user"] = users.First();
            return View();
        }

        [SessionTimeOutJsonFilter]
        public JsonResult resetPassword(int id)
        {
            try
            {
                var user = db.ei_users.Single(u => u.id == id);
                user.password = MyUtils.getMD5("000000");
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                return Json(new SimpleResultModel() { suc = false, msg = ex.Message });                
            }
            return Json(new SimpleResultModel() { suc = true, msg = "密码成功重置为6个0" });
        }

        [SessionTimeOutJsonFilter]
        public JsonResult activeUser(int id)
        {
            string msg;
            try
            {
                var user = db.ei_users.Single(u => u.id == id);
                if (user.forbit_flag == true)
                {
                    user.forbit_flag = false;
                    user.last_login_date = DateTime.Now;
                    msg = "用户成功激活";
                }
                else
                {
                    user.forbit_flag = true;
                    msg = "用户成功禁用";
                }
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                return Json(new SimpleResultModel() { suc = false, msg = ex.Message });
            }
            return Json(new SimpleResultModel() { suc = true, msg = msg });
        }
        #endregion

        #region 情景变换
        [SessionTimeOutJsonFilter]
        [AuthorityFilter]
        public JsonResult ChangeUser(string card_no)
        {
            if (db.ei_users.Where(u => u.card_number == card_no).Count() != 1)
            {
                return Json(new SimpleResultModel() { suc = false, msg = "厂牌不存在或者存在多用户" });
            }
            var user = db.ei_users.Single(u => u.card_number == card_no);
            setcookie(user, 1);
            ClearUserInfoDetail();
            return Json(new SimpleResultModel() { suc = true, msg = "操作成功" });
        }
        #endregion

        #region 权限管理

        [SessionTimeOutFilter]
        [AuthorityFilter]
        public ActionResult Authorities(string searchContent)
        {
            if (searchContent == null) searchContent = "";
            ViewData["searchContent"] = searchContent;
            ViewData["list"] = (from a in db.ei_authority
                                where a.name.Contains(searchContent)
                                || a.en_name.Contains(searchContent)
                                || a.controler_name.Contains(searchContent)
                                || a.action_name.Contains(searchContent)
                                orderby a.number
                                select a).ToList();
            WriteEventLog("权限管理", "获取权限列表,search:"+searchContent);
            return View();
        }

        [SessionTimeOutFilter]
        [HttpPost]
        public ActionResult SaveAuthority(FormCollection fc)
        {
            string number = fc.Get("aut_no").Trim();
            string name = fc.Get("aut_name").Trim();
            string nameEn = fc.Get("aut_en_name").Trim();
            string controlerName = fc.Get("aut_controller").Trim();
            string actionName = fc.Get("aut_action").Trim();
            string iconcls = fc.Get("aut_icon").Trim();
            string description = fc.Get("aut_description").Trim();
            string idStr = fc.Get("aut_id").Trim();            

            try
            {
                //新增
                if (string.IsNullOrEmpty(idStr))
                {
                    if (db.ei_authority.Where(a => a.name == name || a.en_name == nameEn).Count() > 0)
                    {
                        WriteEventLog("权限管理", "该权限已存在，新增失败", -10);
                        ViewBag.tip = "权限已存在，新增失败";
                        return View("Error");
                    }
                    db.ei_authority.Add(new ei_authority()
                    {
                        number = decimal.Parse(number),
                        name = name,
                        en_name = nameEn,
                        controler_name = controlerName,
                        action_name = actionName,
                        iconcls = iconcls,
                        description = description
                    });
                    WriteEventLog("权限管理", "新增权限：" + number + ";" + name);
                }
                else
                {
                    //修改
                    int id = Int32.Parse(idStr);
                    var aut = db.ei_authority.Single(a => a.id == id);
                    aut.number = decimal.Parse(number);
                    aut.name = name;
                    aut.en_name = nameEn;
                    aut.controler_name = controlerName;
                    aut.action_name = actionName;
                    aut.iconcls = iconcls;
                    aut.description = description;
                    WriteEventLog("权限管理", "修改权限：" + number + ";" + name);
                }
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                ViewBag.tip = ex.Message;
                return View("Error");
            }

            return RedirectToAction("Authorities");
        }

        [SessionTimeOutFilter]
        [HttpGet]
        public ActionResult RemoveAuthority(int id)
        {

            if (db.ei_groupAuthority.Where(g => g.authority_id == id).Count() > 0)
            {
                ViewBag.tip = "权限存在于分组中，不能删除";
                return View("Error");
            }

            try
            {
                var aut = db.ei_authority.Single(a => a.id == id);
                db.ei_authority.Remove(aut);
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                WriteEventLog("权限管理", "删除失败：" + ex.Message, -10);
                ViewBag.tip = ex.Message;
                return View("Error");
            }

            WriteEventLog("权限管理", "删除权限" + id);

            return RedirectToAction("Authorities");
        }
        #endregion

        #region 分组管理


        [SessionTimeOutFilter]
        [AuthorityFilter]
        public ActionResult Groups()
        {
            return View();
        }

        [SessionTimeOutJsonFilter]
        public JsonResult GetGroups(string searchContent)
        {
            if (string.IsNullOrEmpty(searchContent)) searchContent = "";
            var result = (from g in db.ei_groups
                          where g.name.Contains(searchContent)
                          || g.description.Contains(searchContent)
                          select new
                          {
                              id = g.id,
                              name = g.name,
                              description = g.description
                          }).ToList();
            return Json(new { suc = true, rows = result });
        }


        [SessionTimeOutFilter]
        public ActionResult GroupDetail(int id)
        {
            var group = db.ei_groups.Single(g => g.id == id);
            ViewData["groupId"] = group.id;
            ViewData["groupName"] = group.name;
            ViewData["groupDes"] = group.description;
            ViewData["groupUsers"] = group.ei_groupUser
                .Select(u => new GroupUserModel() { id = u.user_id, cardNo = u.ei_users.card_number, userName = u.ei_users.name })
                .ToList();
            ViewData["groupAuts"] = group.ei_groupAuthority
                .Select(a => new GroupAutModel() { id = a.authority_id,autNumber=a.ei_authority.number, autName = a.ei_authority.name, autDes = a.ei_authority.description })
                .ToList();
            return View();
        }

        [SessionTimeOutJsonFilter]
        public JsonResult AddGroup(string name, string des)
        {
            if (db.ei_groups.Where(g => g.name == name).Count() > 0)
            {
                return Json(new SimpleResultModel() { suc = false, msg = "组名已存在" });
            }
            db.ei_groups.Add(new ei_groups() { name = name, description = des });
            db.SaveChanges();
            return Json(new SimpleResultModel() { suc = true, msg = "分组创建成功" });
        }

        [SessionTimeOutJsonFilter]
        public JsonResult UpdateGroup(int id,string name, string des)
        {
            try
            {
                var group = db.ei_groups.Single(g => g.id == id);
                group.name = name;
                group.description = des;
                db.SaveChanges();
                return Json(new SimpleResultModel() { suc = true, msg = "保存成功" });
            }
            catch (Exception ex)
            {
                return Json(new SimpleResultModel() { suc = false, msg = "保存失败:"+ex.Message });                
            }
        }


        [SessionTimeOutFilter]
        public ActionResult RemoveGroup(int id)
        {
            try
            {
                var group = db.ei_groups.Single(g => g.id == id);
                foreach (var u in group.ei_groupUser)
                {
                    db.ei_groupUser.Remove(u);
                }
                foreach (var a in group.ei_groupAuthority)
                {
                    db.ei_groupAuthority.Remove(a);
                }
                db.ei_groups.Remove(group);
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                ViewBag.tip = ex.Message;
                return View("Error");
            }
            return RedirectToAction("Groups");
        }

        [SessionTimeOutJsonFilter]
        public JsonResult AddUserToGroup(int group_id, int user_id) {
            try
            {
                if (db.ei_groupUser.Where(gu => gu.group_id == group_id && gu.user_id == user_id).Count() > 0)
                {
                    return Json(new { result = new SimpleResultModel() { suc = false, msg = "该用户已存在分组中" } });
                }
                var groupUser = new ei_groupUser();
                groupUser.group_id = group_id;
                groupUser.user_id = user_id;
                db.ei_groupUser.Add(groupUser);
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                return Json(new { result = new SimpleResultModel() { suc = false, msg = "添加用户失败:" + ex.Message } });
            }
            GroupUserModel groupU = db.ei_users.Where(u => u.id == user_id)
                .Select(u => new GroupUserModel() { id = u.id, cardNo = u.card_number, userName = u.name }).First();

            return Json(new { result = new SimpleResultModel() { suc = true, msg = "添加用户成功" }, groupUser = groupU });
        }

        [SessionTimeOutJsonFilter]
        public JsonResult RemoveUserInGroup(int group_id, string user_ids)
        {
            string[] userIdArr = user_ids.Split(',');
            try
            {
                foreach (var userIdStr in userIdArr) {
                    int userId = Int32.Parse(userIdStr);
                    var groupUser = db.ei_groupUser.Single(gu => gu.group_id == group_id && gu.user_id == userId);
                    db.ei_groupUser.Remove(groupUser);
                }
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                return Json(new SimpleResultModel() { suc = false, msg = "用户移除失败：" + ex.Message });
            }
            return Json(new SimpleResultModel() { suc = true, msg = "用户移除成功,数量："+userIdArr.Count() });
        }

        [SessionTimeOutJsonFilter]
        public JsonResult AddAutToGroup(int group_id, int aut_id)
        {
            try
            {
                if (db.ei_groupAuthority.Where(ga => ga.group_id == group_id && ga.authority_id == aut_id).Count() > 0)
                {
                    return Json(new { result = new SimpleResultModel() { suc = false, msg = "权限已存在分组中" } });
                }
                var groupAut = new ei_groupAuthority();
                groupAut.group_id = group_id;
                groupAut.authority_id = aut_id;
                db.ei_groupAuthority.Add(groupAut);
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                return Json(new { result = new SimpleResultModel() { suc = false, msg = "权限添加失败：" + ex.Message } });
            }
            GroupAutModel groupA = db.ei_authority.Where(a => a.id == aut_id)
                .Select(a => new GroupAutModel() { id = a.id, autNumber = a.number, autName = a.name, autDes = a.description }).First();
            return Json(new { result = new SimpleResultModel() { suc = true, msg = "权限添加成功" }, groupAut = groupA });
        }

        [SessionTimeOutJsonFilter]
        public JsonResult RemoveAutInGroup(int group_id, string aut_ids)
        {
            string[] autIdArr = aut_ids.Split(',');
            try
            {
                foreach (string autIdStr in autIdArr)
                {
                    int autId = Int32.Parse(autIdStr);
                    var groupAut = db.ei_groupAuthority.Single(ga => ga.group_id == group_id && ga.authority_id == autId);
                    db.ei_groupAuthority.Remove(groupAut);
                }
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                return Json(new SimpleResultModel() { suc = false, msg = "权限移除失败：" + ex.Message });
            }
            return Json(new SimpleResultModel() { suc = true, msg = "权限移除成功" });
        }

        #endregion

        #region PO单归属


        [SessionTimeOutFilter]
        [AuthorityFilter]
        public ActionResult POAccountInfo()
        {
            return View();
        }

        [SessionTimeOutJsonFilter]
        public JsonResult GetPOInfo(string poNumber)
        {
            var result = db.GetPOAccount(poNumber).First();
            if (string.IsNullOrEmpty(result.account))
            {
                return Json(new SimpleResultModel() { suc = false, msg = "此PO不存在" });
            }
            else
            {
                return Json(new SimpleResultModel() { suc = true, msg = result.account + ":" + ((DateTime)result.poDate).ToString("yyyy-MM-dd") });
            }


        }

        #endregion

    }
}
