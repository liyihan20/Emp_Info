//------------------------------------------------------------------------------
// <auto-generated>
//    此代码是根据模板生成的。
//
//    手动更改此文件可能会导致应用程序中发生异常行为。
//    如果重新生成代码，则将覆盖对此文件的手动更改。
// </auto-generated>
//------------------------------------------------------------------------------

namespace EmpInfo.Models
{
    using System;
    using System.Collections.Generic;
    
    public partial class ei_mqHRTalkRecord
    {
        public int id { get; set; }
        public string sys_no { get; set; }
        public System.DateTime in_time { get; set; }
        public Nullable<System.DateTime> talk_time { get; set; }
        public string t_status { get; set; }
        public string talk_result { get; set; }
    }
}
