using EmpInfo.Filter;
using EmpInfo.Models;
using EmpInfo.Util;
using System;
using System.Linq;
using System.Web.Mvc;

namespace EmpInfo.Controllers
{
    /// <summary>
    /// 知识库模块，刚通知上线就夭折了，上级担心泄密
    /// </summary>
    public class KWController : BaseController
    {
        /// <summary>
        /// 新建
        /// </summary>
        /// <returns></returns>
        [SessionTimeOutFilter]
        public ActionResult Create()
        {
            if (!isAdmin()) {
                ViewBag.tip = "没有管理权限";
                return View("Error");
            }

            ViewData["item"] = new kw_items()
            {
                item_no = GetNextSysNum("KW")
            };
            ViewData["catalogs"] = db.kw_catalogs.Select(c => c.name).ToList();
            return View();
        }

         //<summary>
         //修改
         //</summary>
         //<param name="itemNo"></param>
         //<returns></returns>
        //[SessionTimeOutFilter]
        //public ActionResult Modify(string itemNo)
        //{
        //    if (!isAdmin()) {
        //        ViewBag.tip = "没有管理权限";
        //        return View("Error");
        //    }

        //    var item = db.kw_items.Where(k => k.item_no == itemNo).FirstOrDefault();
        //    if (item == null) {
        //        ViewBag.tip = "此文档不存在";
        //        return View("Error");
        //    }
        //    if (!item.creater_no.Equals(userInfo.cardNo) && !(item.users_can_update ?? "").Contains(userInfo.cardNo)) {
        //        ViewBag.tip = "你没有修改此文档的权限";
        //        return View("Error");
        //    }
        //    ViewData["item"] = item;
        //    ViewData["catalogs"] = db.kw_catalogs.Select(c => c.name).ToList();

        //    return View("Create");
        //}

        /// <summary>
        /// 保存
        /// </summary>
        /// <param name="fc"></param>
        /// <returns></returns>
        [SessionTimeOutJsonFilter]
        [ValidateInput(false)]
        public JsonResult Save(FormCollection fc)
        {
            try {
                kw_items item = new kw_items();
                MyUtils.SetFieldValueToModel(fc, item);

                bool needToGenerateKey = true; //是否需要重新生成关键字索引                
                var existItem = db.kw_items.Where(k => k.item_no == item.item_no).FirstOrDefault();
                if (existItem != null) {
                    if (existItem.item_keys.Equals(item.item_keys)) {
                        needToGenerateKey = false; //没有改动，不需要重新生成
                    }
                    existItem.caption = item.caption;
                    existItem.catalog = item.catalog;
                    existItem.users_can_update = item.users_can_update;
                    existItem.item_keys = item.item_keys;
                    existItem.text_content = item.text_content;
                    existItem.has_attachment = item.has_attachment;                    
                    existItem.last_update_time = DateTime.Now;
                }
                else {
                    item.create_time = DateTime.Now;
                    item.creater_name = userInfo.name;
                    item.creater_no = userInfo.cardNo;
                    item.last_update_time = DateTime.Now;
                    item.open_flag = false;

                    db.kw_items.Add(item);
                }

                db.kw_updateLog.Add(new kw_updateLog()
                {
                    item_no = item.item_no,
                    update_time = DateTime.Now,
                    update_user = userInfo.name + "(" + userInfo.cardNo + ")"
                });

                // 生成关键字
                if (needToGenerateKey && !string.IsNullOrWhiteSpace(item.item_keys)) {
                    var keys = item.item_keys.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (keys.Count() > 10) {
                        return Json(new SimpleResultModel(false, "关键字数量不能大于10个"));
                    }
                    foreach (var k in db.kw_itemKey.Where(i => i.item_no == item.item_no).ToList()) {
                        db.kw_itemKey.Remove(k);
                    }
                    foreach (var key in keys) {
                        if (key.Length > 10) {
                            return Json(new SimpleResultModel(false, "关键字的长度不能超过10个字节：" + key));
                        }
                        else if (key.Length < 2) {
                            return Json(new SimpleResultModel(false, "关键字的长度不能少于2个字节：" + key));
                        }
                        db.kw_itemKey.Add(new kw_itemKey()
                        {
                            item_no = item.item_no,
                            item_key = key
                        });
                    }
                }
                db.SaveChanges();
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel(ex));
            }
            return Json(new SimpleResultModel(true));

        }

        /// <summary>
        /// 查看
        /// </summary>
        /// <param name="itemNo"></param>
        /// <returns></returns>
        [SessionTimeOutFilter]
        public ActionResult Check(string itemNo)
        {
            var item = db.kw_items.Where(k => k.item_no == itemNo).FirstOrDefault();
            if (item == null) {
                ViewBag.tip = "此文档不存在";
                return View("Error");
            }
            ViewData["item"] = item;
            WriteEventLog("查看文档", item.item_no);
            return View();
        }

        /// <summary>
        /// 搜索查询主界面
        /// </summary>
        /// <returns></returns>
        //[SessionTimeOutFilter]
        //public ActionResult KWIndex()
        //{
        //    ViewData["catalogs"] = db.kw_catalogs.Select(k => k.name).ToList();
        //    ViewData["isAdmin"] = isAdmin();

        //    return View();
        //}

        /// <summary>
        /// 用户搜索
        /// </summary>
        /// <param name="catalog">类别</param>
        /// <param name="content">搜索内容</param>
        /// <returns></returns>
        public JsonResult Search(string catalog, string content)
        {
            var result = db.kw_items.Where(k => k.open_flag == true).Select(k => new KWSearchResultModels()
            {
                item_no = k.item_no,
                caption = k.caption,
                catalog = k.catalog,
                creater_name = k.creater_name,
                create_time = k.create_time,
                last_update_time = k.last_update_time,
                has_attachment = k.has_attachment
            });
            if (!catalog.Equals("所有")) {
                result = result.Where(k => k.catalog == catalog);
            }
            if (!string.IsNullOrWhiteSpace(content)) {
                // 标题匹配
                result = result.Where(k => k.caption.Contains(content));
                // 查询关键字匹配
                result = result.Union(
                    from kw in db.kw_items
                    join ky in db.kw_itemKey on kw.item_no equals ky.item_no
                    where content.Contains(ky.item_key)
                    && kw.open_flag == true
                    && (catalog == "所有" || kw.catalog == catalog)
                    select new KWSearchResultModels()
                    {
                        item_no = kw.item_no,
                        caption = kw.caption,
                        catalog = kw.catalog,
                        creater_name = kw.creater_name,
                        create_time = kw.create_time,
                        last_update_time = kw.last_update_time,
                        has_attachment = kw.has_attachment
                    });
            }

            return Json(result.OrderByDescending(r => r.last_update_time).ToList());
        }

        /// <summary>
        /// 我的列表
        /// </summary>
        /// <returns></returns>
        //[SessionTimeOutFilter]
        //public ActionResult MyList()
        //{
        //    if (!isAdmin()) {
        //        ViewBag.tip = "没有管理权限";
        //        return View("Error");
        //    }
        //    return View();
        //}

        //public JsonResult GetMyList()
        //{
        //    var result = (db.kw_items.Where(k => k.creater_no == userInfo.cardNo || k.users_can_update.Contains("(" + userInfo.cardNo + ")"))
        //        .Select(r => new KWSearchResultModels()
        //    {
        //        item_no = r.item_no,
        //        caption = r.caption,
        //        catalog = r.catalog,
        //        creater_name = r.creater_name,
        //        create_time = r.create_time,
        //        last_update_time = r.last_update_time,
        //        has_attachment = r.has_attachment,
        //        open_flag = r.open_flag
        //    }).OrderByDescending(r => r.last_update_time)).Distinct().ToList();

        //    return Json(result, JsonRequestBehavior.AllowGet);
        //}

        /// <summary>
        /// 删除
        /// </summary>
        /// <param name="itemNo">文档编号</param>
        /// <returns></returns>
        public JsonResult Delete(string itemNo)
        {
            var item = db.kw_items.Where(k => k.item_no == itemNo).FirstOrDefault();
            if (item == null) {
                return Json(new SimpleResultModel(false, "此文档已被删除"));
            }
            try {
                db.kw_items.Remove(item);
                db.SaveChanges();

                WriteEventLog("知识库", "删除文档：" + item.item_no + ";" + item.caption);
                return Json(new SimpleResultModel(true));
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel(ex));
            }
        }

        /// <summary>
        /// 切换发布、未发布状态
        /// </summary>
        /// <param name="itemNo"></param>
        /// <returns></returns>
        public JsonResult ToggleFlag(string itemNo)
        {
            var item = db.kw_items.Where(k => k.item_no == itemNo).FirstOrDefault();
            if (item == null) {
                return Json(new SimpleResultModel(false, "此文档已被删除"));
            }
            try {
                item.open_flag = !item.open_flag;
                db.SaveChanges();

                WriteEventLog("知识库", "切换文档状态：" + item.item_no);
                return Json(new SimpleResultModel(true));
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel(ex));
            }
        }

        /// <summary>
        /// 是否有管理权限
        /// </summary>
        /// <returns></returns>
        public bool isAdmin()
        {
            bool isAdmin;
            if (Session["isKWAdmin"] == null) {
                isAdmin = db.ei_flowAuthority.Where(f => f.bill_type == "KW" && f.relate_type == "管理文档" && f.relate_value == userInfo.cardNo).Count() > 0;
                Session["isKWAdmin"] = isAdmin;
            }
            else {
                isAdmin = (bool)Session["isKWAdmin"];
            }
            return isAdmin;
        }

    }
}
