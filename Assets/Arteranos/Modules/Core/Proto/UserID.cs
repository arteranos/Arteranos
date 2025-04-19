using Ipfs;
using Ipfs.Cryptography.Proto;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;

namespace Arteranos.Core
{
    [ProtoContract]
    [TypeConverter(typeof(UserIDConverter))]
    public class UserID : IEquatable<UserID>
    {
        // Has to be there, for serialization.
        [ProtoMember(1)]
        public PublicKey SignPublicKey = null;

        [ProtoMember(2)]
        public string Nickname = null;

        [ProtoMember(3)]
        public string IconCid = null;

        public UserID()
        {

        }

        public UserID(PublicKey SignPublicKey, string Nickname)
        {
            this.SignPublicKey = SignPublicKey;
            this.Nickname = Nickname;
        }

        public UserID(PublicKey SignPublicKey, string Nickname, string IconCid)
        {
            this.SignPublicKey = SignPublicKey;
            this.Nickname = Nickname;
            this.IconCid = IconCid;
        }

        public byte[] Serialize()
        {
            using MemoryStream ms = new();
            Serializer.Serialize(ms, this);
            return ms.ToArray();
        }

        public static UserID Deserialize(byte[] data)
            => Serializer.Deserialize<UserID>(new MemoryStream(data));

        public byte[] Fingerprint => CryptoHelpers.GetFingerprint(SignPublicKey);

        public bool Equals(UserID other)
        {
            if(other?.SignPublicKey == null || SignPublicKey == null) return false;

            return SignPublicKey == other.SignPublicKey;
        }

        public static implicit operator PublicKey(UserID userID) => userID?.SignPublicKey;

        public static implicit operator string(UserID userID) => userID?.Nickname;

        public static explicit operator Cid(UserID userID) => (userID?.IconCid != null) ? Cid.Decode(userID.IconCid) : null;

        public override bool Equals(object obj) => Equals(obj as UserID);
        public override int GetHashCode()
        {
            HashCode hc = new();
            foreach(byte b in SignPublicKey.Serialize()) hc.Add(b);
            return hc.ToHashCode();
        }

        public static bool operator ==(UserID left, UserID right) => EqualityComparer<UserID>.Default.Equals(left, right);
        public static bool operator !=(UserID left, UserID right) => !(left == right);
    }

    public class UserIDConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            if(sourceType == typeof(string)) return true;
            return base.CanConvertFrom(context, sourceType);
        }

        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            if(destinationType == typeof(string)) return true;
            return base.CanConvertTo(context, destinationType);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if(value is string s)
            {
                byte[] bytes = Convert.FromBase64String(s);
                return UserID.Deserialize(bytes);
            }
            else
                return base.ConvertFrom(context, culture, value);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string))
            {
                byte[] bytes = ((UserID)value).Serialize();
                return Convert.ToBase64String(bytes);
            }
            else
                return base.ConvertTo(context, culture, value, destinationType);
        }
    }
}