using System;
namespace EmpInfo.Models
{
    public class KWSearchResultModels
    {
        public string item_no { get; set; }
        public string caption { get; set; }
        public string catalog { get; set; }
        public string creater_name { get; set; }
        public DateTime? create_time { get; set; }
        public DateTime? last_update_time { get; set; }
        public bool? has_attachment { get; set; }
        public bool open_flag { get; set; }
    }
}