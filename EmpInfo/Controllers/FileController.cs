using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Configuration;
using System.IO;
using EmpInfo.Models;
using EmpInfo.Util;
using System.Data;
using ExcelDataReader;
using Newtonsoft.Json;

namespace EmpInfo.Controllers
{
    public class FileController : BaseController
    {

        [AcceptVerbs(HttpVerbs.Post)]
        public JsonResult BeginUpload(HttpPostedFileBase file,string sysNum)
        {
            try {
                string folder = MyUtils.GetAttachmentFolder(sysNum);
                if (!Directory.Exists(folder)) {
                    Directory.CreateDirectory(folder);
                }
                file.SaveAs(Path.Combine(folder, file.FileName));
            }
            catch (Exception ex) {
                WriteEventLog("上传附件", "失败：" + sysNum + ";error:" + ex.Message);
                return Json(new SimpleResultModel() { suc = false, msg = ex.Message });
            }
            WriteEventLog("上传附件", "成功：" + sysNum);
            return Json(new SimpleResultModel() { suc = true });
        }

        public JsonResult RemoveUploadedFile(string sysNum, string fileName)
        {
            try {
                string fileDirectory = Path.Combine(MyUtils.GetAttachmentFolder(sysNum), fileName);
                if (System.IO.File.Exists(fileDirectory)) {
                    System.IO.File.Delete(fileDirectory);
                }
                WriteEventLog("删除附件", "成功：" + sysNum + ":" + fileName);
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel() { suc = false, msg = ex.Message });
            }
            return Json(new SimpleResultModel() { suc = true });
        }

        public FileStreamResult DownLoadFile(string sysNum, string fileName)
        {
            try {
                string fileDirectory = Path.Combine(MyUtils.GetAttachmentFolder(sysNum), fileName);
                if (System.IO.File.Exists(fileDirectory)) {
                    return File(new FileStream(fileDirectory, FileMode.Open), "application/octet-stream", Server.UrlEncode(fileName));
                }
                else {
                    return null;
                }
            }
            catch {
                return null;
            }
        }
        

        //厂房简介中上传平面图图片到数据库
        [AcceptVerbs(HttpVerbs.Post)]
        public JsonResult BeginUploadDSImg(HttpPostedFileBase file, int detailId)
        {
            try {
                byte[] imgByteArr = new byte[file.ContentLength];
                file.InputStream.Read(imgByteArr, 0, file.ContentLength);

                var detail = db.ei_bus_place_detail.Where(d => d.id == detailId).FirstOrDefault();
                if (detail == null) {
                    return Json(new SimpleResultModel(false, "楼层不存在，可能已被删除，请刷新后再操作"));
                }
                detail.pic = imgByteArr;
                detail.pic_name = file.FileName;
                db.SaveChanges();
            }
            catch (Exception ex) {
                WriteEventLog("上传厂房平面图", "失败：" + detailId + ";error:" + ex.Message);
                return Json(new SimpleResultModel() { suc = false, msg = ex.Message });
            }
            WriteEventLog("上传厂房平面图", "成功：" + detailId);
            return Json(new SimpleResultModel() { suc = true });
        }

        //放行条流程，读取放行物品excel
        public JsonResult FXReadExcelData(string sysNum,string fileName)
        {
            List<ei_fxApplyEntry> list = new List<ei_fxApplyEntry>();

            var dt = new DataSet();
            using (var stream = System.IO.File.Open(Path.Combine(MyUtils.GetAttachmentFolder(sysNum), fileName), FileMode.Open, FileAccess.Read)) {
                using (var reader = ExcelReaderFactory.CreateReader(stream)) {
                    dt = reader.AsDataSet();
                }
            }
            var tb = dt.Tables[0];
            DataRow r;
            decimal qtyTmp;
            //物品名称 物品型号 数量 单位 备注
            for (var i = 1; i < tb.Rows.Count; i++) {
                r = tb.Rows[i];
                ei_fxApplyEntry en = new ei_fxApplyEntry();

                if (!decimal.TryParse(Convert.ToString(r[2]), out qtyTmp)) {
                    return Json(new SimpleResultModel(false, "存在不合法的数量[" + Convert.ToString(r[2]) + "]，行号：" + (i + 1)));
                }

                en.item_name = Convert.ToString(r[0]);
                en.item_model = Convert.ToString(r[1]);
                en.item_qty = qtyTmp;
                en.item_unit = Convert.ToString(r[3]);
                en.comment = Convert.ToString(r[4]);

                list.Add(en);
            }

            return Json(new SimpleResultModel(true,"读取成功",JsonConvert.SerializeObject(list)));
        }

    }
}
