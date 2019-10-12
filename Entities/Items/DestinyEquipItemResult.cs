using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Destiny2.Responses
{
    public class DestinyEquipItemResult
    {
        [JsonProperty(PropertyName = "itemInstanceId")]
        public long ItemInstanceId { get; set; }

        [JsonProperty(PropertyName = "equipStatus")]
        public int EquipStatus { get; set; }
    }
}
