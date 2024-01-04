using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WT_Transfer.Models
{
    public class ContactShow
    {
        public string _id { get; set; }
        public List<KeyValuePairModel> addresses { get; set; }
        public List<KeyValuePairModel> emails { get; set; }
        public List<KeyValuePairModel> ims { get; set; }
        public string nickname { get; set; }
        public string note { get; set; }
        public Dictionary<string, List<List<object>>> organizations { get; set; }
        public List<KeyValuePairModel> phoneNumbers { get; set; }
        public List<KeyValuePairModel> sipAddresses { get; set; }
        public List<string> structuredName { get; set; }
        public List<KeyValuePairModel> websites { get; set; }

        public ContactShow(Contact contact) {
            _id = contact._id;
            nickname = contact.nickname;
            note = contact.note;
            organizations = contact.organizations;
            structuredName = contact.structuredName;

            addresses =
                contact.addresses.Select
                (kv => new KeyValuePairModel { Key = kv.Key, Value = kv.Value }).ToList();
            emails =
                contact.emails.Select
                (kv => new KeyValuePairModel { Key = kv.Key, Value = kv.Value }).ToList();
            ims =
                contact.ims.Select
                (kv => new KeyValuePairModel { Key = kv.Key, Value = kv.Value }).ToList();
            phoneNumbers =
                contact.phoneNumbers.Select
                (kv => new KeyValuePairModel { Key = kv.Key, Value = kv.Value }).ToList();
            sipAddresses =
                contact.sipAddresses.Select
                (kv => new KeyValuePairModel { Key = kv.Key, Value = kv.Value }).ToList();
            websites =
                contact.websites.Select
                (kv => new KeyValuePairModel { Key = kv.Key, Value = kv.Value }).ToList();
        }
    }
}
