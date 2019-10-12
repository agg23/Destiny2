using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Destiny2.Responses
{
    public class DestinyEquipItemResponse
    {
        [JsonProperty(PropertyName = "equipResults")]
        public IEnumerable<DestinyEquipItemResult> EquipResults { get; set; } = null;
    }
}
