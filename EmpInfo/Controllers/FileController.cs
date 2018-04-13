using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Configuration;
using System.IO;
using EmpInfo.Models;
using EmpInfo.Util;

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
        

    }
}
