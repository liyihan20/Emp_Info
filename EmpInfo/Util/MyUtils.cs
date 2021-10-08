using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using EmpInfo.Models;
using System.Linq;
using System.Text.RegularExpressions;
using Gma.QrCodeNet.Encoding;
using Gma.QrCodeNet.Encoding.Windows.Render;
using System.Configuration;
using System.Collections.Generic;
using System.Web;


namespace EmpInfo.Util
{
    public class MyUtils
    {
        private static string AES_128_key = "^*xxzx2018xlej*^";

        //生成随机数列
        public static string CreateValidateNumber(int length)
        {
            //去掉数字0和字母o，因为不容易区分
            string Vchar = "1,2,3,4,5,6,7,8,9,a,b,c,d,e,f,g,h,i,j,k,l,m,n,p" +
            ",q,r,s,t,u,v,w,x,y,z,A,B,C,D,E,F,G,H,I,J,K,L,M,N,P,Q" +
            ",R,S,T,U,V,W,X,Y,Z";

            string[] VcArray = Vchar.Split(new Char[] { ',' });//拆分成数组
            string num = "";

            int temp = -1;//记录上次随机数值，尽量避避免生产几个一样的随机数

            Random rand = new Random();
            //采用一个简单的算法以保证生成随机数的不同
            for (int i = 1; i < length + 1; i++) {
                if (temp != -1) {
                    rand = new Random(i * temp * unchecked((int)DateTime.Now.Ticks));
                }

                int t = rand.Next(VcArray.Length - 1);
                if (temp != -1 && temp == t) {
                    return CreateValidateNumber(length);

                }
                temp = t;
                num += VcArray[t];
            }
            return num;
        }

        public static byte[] CreateValidateGraphic(string validateCode)
        {
            Bitmap image = new Bitmap((int)Math.Ceiling(validateCode.Length * 18.0), 26);
            Graphics g = Graphics.FromImage(image);
            try {
                //生成随机生成器
                Random random = new Random();
                //清空图片背景色
                g.Clear(Color.White);
                //画图片的干扰线
                for (int i = 0; i < 25; i++) {
                    int x1 = random.Next(image.Width);
                    int x2 = random.Next(image.Width);
                    int y1 = random.Next(image.Height);
                    int y2 = random.Next(image.Height);
                    g.DrawLine(new Pen(Color.Silver), x1, y1, x2, y2);
                }
                Font font = new Font("Arial", 16, (FontStyle.Bold | FontStyle.Italic));
                LinearGradientBrush brush = new LinearGradientBrush(new Rectangle(0, 0, image.Width, image.Height),
                 Color.Blue, Color.DarkRed, 1.2f, true);
                g.DrawString(validateCode, font, brush, 3, 2);
                //画图片的前景干扰点
                for (int i = 0; i < 100; i++) {
                    int x = random.Next(image.Width);
                    int y = random.Next(image.Height);
                    image.SetPixel(x, y, Color.FromArgb(random.Next()));
                }
                //画图片的边框线
                g.DrawRectangle(new Pen(Color.Silver), 0, 0, image.Width - 1, image.Height - 1);
                //保存图片数据
                MemoryStream stream = new MemoryStream();
                image.Save(stream, ImageFormat.Jpeg);
                //输出图片流
                return stream.ToArray();
            }
            finally {
                g.Dispose();
                image.Dispose();
            }
        }

        public static string getMD5(string str)
        {
            if (str.Length > 2) {
                str = "Who" + str.Substring(2) + "Are" + str.Substring(0, 2) + "You";
            }
            else {
                str = "Who" + str + "Are" + str + "You";
            }
            return getNormalMD5(str);

        }

        public static string getNormalMD5(string str)
        {
            MD5 md5 = new MD5CryptoServiceProvider();
            byte[] data = Encoding.Default.GetBytes(str);
            byte[] result = md5.ComputeHash(data);
            String ret = "";
            for (int i = 0; i < result.Length; i++) {
                ret += result[i].ToString("x").PadLeft(2, '0');
            }
            return ret;
        }

        //将中文编码为utf-8
        public static string EncodeToUTF8(string str)
        {
            string result = System.Web.HttpUtility.UrlEncode(str, System.Text.Encoding.GetEncoding("UTF-8"));
            return result;
        }

        //将utf-8解码
        public static string DecodeToUTF8(string str)
        {
            string result = System.Web.HttpUtility.UrlDecode(str, System.Text.Encoding.GetEncoding("UTF-8"));
            return result;
        }

        //权限判断
        public static bool hasGotPower(int userId, string controlerName, string actionName)
        {
            ICAuditEntities db = new ICAuditEntities();
            try {
                var power = from a in db.ei_authority
                            from u in db.ei_groups
                            from ga in a.ei_groupAuthority
                            from gu in u.ei_groupUser
                            where ga.group_id == u.id
                            && gu.user_id == userId
                            && a.controler_name == controlerName
                            && a.action_name == actionName
                            select a;
                if (power.Count() > 0)
                    return true;
                else
                    return false;
            }
            catch {
                return false;
            }

        }

        //周几的转换
        public static string GetWeekDay(DayOfWeek dw)
        {
            string result = ""; ;
            switch (dw) {
                case DayOfWeek.Monday:
                    result = WeekDay.周一.ToString();
                    break;
                case DayOfWeek.Tuesday:
                    result = WeekDay.周二.ToString();
                    break;
                case DayOfWeek.Wednesday:
                    result = WeekDay.周三.ToString();
                    break;
                case DayOfWeek.Thursday:
                    result = WeekDay.周四.ToString();
                    break;
                case DayOfWeek.Friday:
                    result = WeekDay.周五.ToString();
                    break;
                case DayOfWeek.Saturday:
                    result = WeekDay.周六.ToString();
                    break;
                case DayOfWeek.Sunday:
                    result = WeekDay.周日.ToString();
                    break;
            }
            return result;
        }

        //早中晚的时间段转化 时间段作为后台参数设置，是变动的，已弃用，改用base controller 同名方法 2016-9-23
        public static string GetTimeSegment(int hour, int minutes = 0)
        {
            string result = "";
            if (hour >= 7 && hour <= 9) {
                result = MealSegment.早餐.ToString();
            }
            else if (hour >= 10 && hour <= 15) {
                result = MealSegment.午餐.ToString();
            }
            else if (hour >= 16 && hour <= 20) {
                result = MealSegment.晚餐.ToString();
            }
            else if (hour >= 21 && hour <= 23) {
                result = MealSegment.宵夜.ToString();
            }

            return result;
        }

        //生成订单编号,因生成的字符串含有特殊字符，已弃用
        public static string GetOrderNo(string prefix = "DN")
        {
            return prefix + DateTime.Now.GetHashCode();
        }

        //从身份证编号读取出生日
        public static string GetBirthdayFromID(string idNumber)
        {
            //从第7位开始截取8位生日，只截取月和日
            //return idNumber.Substring(6, 4) + "-" + idNumber.Substring(10, 2) + "-" + idNumber.Substring(12, 2);
            return idNumber.Substring(10, 2) + "-" + idNumber.Substring(12, 2);
        }

        public static byte[] GetServerImage(string url)
        {
            Image im = Image.FromFile(url);
            using (MemoryStream ms = new MemoryStream()) {
                im.Save(ms, ImageFormat.Png);
                byte[] portrait = new byte[ms.Length];
                ms.Seek(0, SeekOrigin.Begin);
                ms.Read(portrait, 0, portrait.Length);
                return portrait;
            }
        }

        //验证登录密码，2013-5-27后开启复杂性密码，强制用户修改。
        public static string ValidatePassword(string str)
        {
            string alph = @"[A-Za-z]+";
            string num = @"\d+";
            //string cha = @"[\-`=\\\[\];',\./~!@#\$%\^&\*\(\)_\+\|\{\}:""<>\?]+";
            if (!new Regex(alph).IsMatch(str)) {
                return "新密码必须包含英文字母，保存失败。英文字母有：A~Z，a~z";
            }
            if (!new Regex(num).IsMatch(str)) {
                return "新密码必须包含阿拉伯数字，保存失败。数字有：0~9";
            }
            //if (!new Regex(cha).IsMatch(str)) {
            //    return @"新密码必须包含特殊字符，保存失败。特殊字符有：-`=\[];',./~!@#$%^&*()_+|{}:""<>?";
            //}
            return "";
        }

        //将保存在数据库的图片二进制转化为Image格式
        public static Image BytesToImage(byte[] buffer)
        {
            using (MemoryStream ms = new MemoryStream(buffer)) {
                Image image = System.Drawing.Image.FromStream(ms);
                return image;
            }
        }

        //生成缩略图
        public static byte[] MakeThumbnail(Image originalImage, int width = 0, int height = 128, string mode = "H")
        {
            int towidth = width;
            int toheight = height;

            int x = 0;
            int y = 0;
            int ow = originalImage.Width;
            int oh = originalImage.Height;

            switch (mode) {
                case "HW"://指定高宽缩放（可能变形）                 
                    break;
                case "W"://指定宽，高按比例                     
                    toheight = originalImage.Height * width / originalImage.Width;
                    break;
                case "H"://指定高，宽按比例 
                    towidth = originalImage.Width * height / originalImage.Height;
                    break;
                case "Cut"://指定高宽裁减（不变形）                 
                    if ((double)originalImage.Width / (double)originalImage.Height > (double)towidth / (double)toheight) {
                        oh = originalImage.Height;
                        ow = originalImage.Height * towidth / toheight;
                        y = 0;
                        x = (originalImage.Width - ow) / 2;
                    }
                    else {
                        ow = originalImage.Width;
                        oh = originalImage.Width * height / towidth;
                        x = 0;
                        y = (originalImage.Height - oh) / 2;
                    }
                    break;
                default:
                    break;
            }

            //新建一个bmp图片 
            Image bitmap = new System.Drawing.Bitmap(towidth, toheight);

            //新建一个画板 
            Graphics g = System.Drawing.Graphics.FromImage(bitmap);

            //设置高质量插值法 
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.High;

            //设置高质量,低速度呈现平滑程度 
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;

            //清空画布并以透明背景色填充 
            g.Clear(Color.Transparent);

            //在指定位置并且按指定大小绘制原图片的指定部分 
            g.DrawImage(originalImage, new Rectangle(0, 0, towidth, toheight),
                new Rectangle(x, y, ow, oh),
                GraphicsUnit.Pixel);

            try {
                byte[] bytes;
                using (MemoryStream ms = new MemoryStream()) {
                    bitmap.Save(ms, originalImage.RawFormat);
                    bytes = ms.ToArray();
                }
                return bytes;
            }
            catch (System.Exception e) {
                throw e;
            }
            finally {
                originalImage.Dispose();
                g.Dispose();
            }
        }

        //生成二维码
        public static byte[] GetQrCode(string content, int size = 9)
        {
            var encoder = new QrEncoder(ErrorCorrectionLevel.M);
            QrCode qrCode = encoder.Encode(content);
            GraphicsRenderer render = new GraphicsRenderer(new FixedModuleSize(size, QuietZoneModules.Two), Brushes.Black, Brushes.White);

            MemoryStream memoryStream = new MemoryStream();
            render.WriteToStream(qrCode.Matrix, ImageFormat.Jpeg, memoryStream);

            return memoryStream.ToArray();
        }

        //生成一维码
        public static byte[] GetCode39(string sourceCode, int barCodeHeight = 80)
        {
            string BarCodeText = sourceCode.ToUpper();
            int leftMargin = 5;
            int topMargin = 0;
            int thickLength = 4;
            int narrowLength = 2;
            int intSourceLength = sourceCode.Length;
            string strEncode = "010010100"; //添加起始码“ *”.            
            string AlphaBet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ-. $/+%*";
            string[] Code39 =
             {
                 /* 0 */ "000110100" , 
                 /* 1 */ "100100001" , 
                 /* 2 */ "001100001" , 
                 /* 3 */ "101100000" ,
                 /* 4 */ "000110001" , 
                 /* 5 */ "100110000" , 
                 /* 6 */ "001110000" , 
                 /* 7 */ "000100101" ,
                 /* 8 */ "100100100" , 
                 /* 9 */ "001100100" , 
                 /* A */ "100001001" , 
                 /* B */ "001001001" ,
                 /* C */ "101001000" , 
                 /* D */ "000011001" , 
                 /* E */ "100011000" , 
                 /* F */ "001011000" ,
                 /* G */ "000001101" , 
                 /* H */ "100001100" , 
                 /* I */ "001001100" , 
                 /* J */ "000011100" ,
                 /* K */ "100000011" , 
                 /* L */ "001000011" , 
                 /* M */ "101000010" , 
                 /* N */ "000010011" ,
                 /* O */ "100010010" , 
                 /* P */ "001010010" , 
                 /* Q */ "000000111" , 
                 /* R */ "100000110" ,
                 /* S */ "001000110" , 
                 /* T */ "000010110" , 
                 /* U */ "110000001" , 
                 /* V */ "011000001" ,
                 /* W */ "111000000" , 
                 /* X */ "010010001" , 
                 /* Y */ "110010000" , 
                 /* Z */ "011010000" ,
                 /* - */ "010000101" , 
                 /* . */ "110000100" , 
                 /*' '*/ "011000100" ,
                 /* $ */ "010101000" ,
                 /* / */ "010100010" , 
                 /* + */ "010001010" , 
                 /* % */ "000101010" , 
                 /* * */ "010010100"  
             };
            sourceCode = sourceCode.ToUpper();
            Bitmap objBitmap = new Bitmap(((thickLength * 3 + narrowLength * 7) * (intSourceLength + 2)) +
                                           (leftMargin * 2), barCodeHeight + (topMargin * 2));
            Graphics objGraphics = Graphics.FromImage(objBitmap);
            objGraphics.FillRectangle(Brushes.White, 0, 0, objBitmap.Width, objBitmap.Height);
            for (int i = 0; i < intSourceLength; i++) {
                //非法字符校验
                if (AlphaBet.IndexOf(sourceCode[i]) == -1 || sourceCode[i] == '*') {
                    objGraphics.DrawString("Invalid Bar Code", SystemFonts.DefaultFont, Brushes.Red, leftMargin, topMargin);
                    return null;
                }
                //编码
                strEncode = string.Format("{0}0{1}", strEncode,
                Code39[AlphaBet.IndexOf(sourceCode[i])]);
            }
            strEncode = string.Format("{0}0010010100", strEncode); //添加结束码“*”
            int intEncodeLength = strEncode.Length;
            int intBarWidth;
            for (int i = 0; i < intEncodeLength; i++) //绘制 Code39 barcode
            {
                intBarWidth = strEncode[i] == '1' ? thickLength : narrowLength;
                objGraphics.FillRectangle(i % 2 == 0 ? Brushes.Black : Brushes.White, leftMargin, topMargin, intBarWidth, barCodeHeight);
                leftMargin += intBarWidth;
            }
            //绘制明码         
            //Font barCodeTextFont = new Font("黑体", 10F);
            //RectangleF rect = new RectangleF(2, barCodeHeight - 20, objBitmap.Width - 4, 20);
            //objGraphics.FillRectangle(Brushes.White, rect);
            //文本对齐
            //StringFormat sf = new StringFormat();
            //sf.Alignment = StringAlignment.Center;
            //objGraphics.DrawString(BarCodeText, barCodeTextFont, Brushes.Black, rect, sf);

            MemoryStream ms = new MemoryStream();
            objBitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);

            return ms.ToArray();
        }

        //AES加密
        public static string AESEncrypt(string toEncrypt)
        {
            byte[] keyArray = UTF8Encoding.UTF8.GetBytes(AES_128_key);
            byte[] toEncryptArray = UTF8Encoding.UTF8.GetBytes(toEncrypt);

            RijndaelManaged rDel = new RijndaelManaged();
            rDel.Key = keyArray;
            rDel.Mode = CipherMode.ECB;
            rDel.Padding = PaddingMode.PKCS7;

            ICryptoTransform cTransform = rDel.CreateEncryptor();
            byte[] resultArray = cTransform.TransformFinalBlock(toEncryptArray, 0, toEncryptArray.Length);

            return Convert.ToBase64String(resultArray, 0, resultArray.Length);
        }

        //AES解密
        public static string AESDecrypt(string toDecrypt)
        {            
            byte[] keyArray = UTF8Encoding.UTF8.GetBytes(AES_128_key);
            byte[] toEncryptArray = Convert.FromBase64String(toDecrypt);

            RijndaelManaged rDel = new RijndaelManaged();
            rDel.Key = keyArray;
            rDel.Mode = CipherMode.ECB;
            rDel.Padding = PaddingMode.PKCS7;

            ICryptoTransform cTransform = rDel.CreateDecryptor();

            byte[] resultArray = cTransform.TransformFinalBlock(toEncryptArray, 0, toEncryptArray.Length);
            return UTF8Encoding.UTF8.GetString(resultArray);

        }

        /// <summary>
        /// 使用反射将表单的值设置到数据库对象中，根据字段名
        /// </summary>
        /// <param name="col">表单</param>
        /// <param name="obj">数据库对象</param>
        public static void SetFieldValueToModel(System.Web.Mvc.FormCollection col, object obj)
        {
            string val = "", pType = "";
            foreach (var p in obj.GetType().GetProperties()) {
                val = col.Get(p.Name);//字段值
                pType = p.PropertyType.FullName;//数据类型
                if (string.IsNullOrEmpty(val) || val.Equals("null")) continue;
                if (pType.Contains("DateTime")) {
                    DateTime dt;
                    if (DateTime.TryParse(val, out dt)) {
                        p.SetValue(obj, dt, null);
                    }
                }
                else if (pType.Contains("Int32")) {
                    int i;
                    if (int.TryParse(val, out i)) {
                        p.SetValue(obj, i, null);
                    }
                }
                else if (pType.Contains("Decimal")) {
                    decimal dm;
                    if (decimal.TryParse(val, out dm)) {
                        p.SetValue(obj, dm, null);
                    }
                }
                else if (pType.Contains("String")) {
                    p.SetValue(obj, val.Trim(), null);
                }
                else if (pType.Contains("Bool")) {
                    bool bl;
                    if (bool.TryParse(val, out bl)) {
                        p.SetValue(obj, bl, null);
                    }
                }
            }
        }

        /// <summary>
        /// 将指定对象的属性值设置到目标对象，名称相同有效
        /// </summary>
        /// <param name="fromObj"></param>
        /// <param name="toObj"></param>
        public static void CopyPropertyValue(object fromObj, object toObj)
        {
            foreach (var p in fromObj.GetType().GetProperties()) {
                if (p.Name.ToUpper().Equals("ID")) continue; //ID不复制，因为是主键
                var tp = toObj.GetType().GetProperties().Where(t => t.Name == p.Name).FirstOrDefault();
                if (tp != null) {
                    if (tp.PropertyType.Equals(p.PropertyType) && p.GetValue(fromObj, null) != null) {
                        tp.SetValue(toObj, p.GetValue(fromObj, null), null);
                    }
                }
            }
        }

        /// <summary>
        /// 获取申请单号所在的附件文件夹
        /// </summary>
        /// <param name="sysNum">申请单号</param>
        /// <returns></returns>
        public static string GetAttachmentFolder(string sysNum)
        {
            return Path.Combine(
                ConfigurationManager.AppSettings["AttachmentPath"],
                sysNum.Substring(0, 2),
                "20" + sysNum.Substring(2, 2),
                sysNum.Substring(4, 2),
                sysNum
            );
        }

        /// <summary>
        /// 获取某申请单号的所有附件列表
        /// </summary>
        /// <param name="sysNo">申请单号</param>
        /// <returns></returns>
        public static List<AttachmentModel> GetAttachmentInfo(string sysNo)
        {
            var list = new List<AttachmentModel>();
            var folder = GetAttachmentFolder(sysNo);

            DirectoryInfo di = new DirectoryInfo(folder);
            if (di.Exists) {
                foreach (FileInfo fi in di.GetFiles()) {
                    if (fi.Name.EndsWith(".db")) {
                        continue; //将目录自动生成的thumb.db文件过滤掉
                    }
                    list.Add(new AttachmentModel()
                    {
                        fileName = fi.Name,
                        fileSize = fi.Length,
                        ext = fi.Extension
                    });
                    
                }
            }
            return list;
        }

        public static void ClearCookie(HttpResponseBase response,HttpSessionStateBase session){
            var cookie = new HttpCookie(ConfigurationManager.AppSettings["cookieName"]);
            cookie.Expires = DateTime.Now.AddDays(-1);
            response.AppendCookie(cookie);
            session.Clear();
        }

        public static string GetTimeStamp()
        {
            return GetTimeStamp(DateTime.Now);
        }

        public static string GetTimeStamp(DateTime when)
        {
            TimeSpan ts = (DateTime)when - new DateTime(1970, 1, 1, 0, 0, 0, 0);
            return Convert.ToInt64(ts.TotalSeconds).ToString();
        }

        public static string Read(string path)
        {
            path = Path.Combine(System.Environment.CurrentDirectory, path);
            if (!File.Exists(path)) {
                return null;
            }
            String line, content = "";
            using (StreamReader sr = new StreamReader(path, Encoding.Default)) {
                while ((line = sr.ReadLine()) != null) {
                    content += line.ToString();
                }
            }

            return content;
        }

        public static void Write(string path, string content)
        {
            path = Path.Combine("D:\\ei_upload", path);
            if (File.Exists(path)) {
                File.Delete(path);
            }
            using (FileStream fs = new FileStream(path, FileMode.Create)) {
                using (StreamWriter sw = new StreamWriter(fs)) {
                    //开始写入
                    sw.Write(content);
                    //清空缓冲区
                    sw.Flush();
                    //关闭流
                }

            }
        }

    }
}