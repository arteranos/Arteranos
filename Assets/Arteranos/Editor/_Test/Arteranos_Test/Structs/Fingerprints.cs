using NUnit.Framework;
using System;
using Arteranos.Core;
using UnityEngine.TestTools;
using System.Threading.Tasks;
using System.Collections;
using Arteranos.Common.Cryptography;

using UnityEngine;
using Arteranos.Common;
using Newtonsoft.Json;

namespace Arteranos.Test.Structs
{
    public class FPTestBase : IFingerprintable
    {
        public byte[] _fpbytes;

        // Raw SHA256
        public byte[] FingerprintBytes => CryptoHelpers.GetFingerprint(_fpbytes);
    }

    public class FPTest1 : FPTestBase
    {

    }

    public class FPTest2 : FPTestBase
    {

    }

    public class Fingerprints
    {
        readonly FPTest1 a = new()
        {
            _fpbytes = new byte[] { 0x00, 0x01, 0x02, 0x03 }
        };
        readonly FPTest1 b = new() // Same type, different content
        {
            _fpbytes = new byte[] { 0x04, 0x05, 0x06, 0x07 }
        };
        readonly FPTest2 c = new() // Different type, same content
        {
            _fpbytes = new byte[] { 0x00, 0x01, 0x02, 0x03 }
        };
        readonly FPTest1 aa = new() // Same as a, but different instance
        {
            _fpbytes = new byte[] { 0x00, 0x01, 0x02, 0x03 }
        };

        [Test]
        public void T001_Construction()
        {
            Fingerprint a_fp = new(a);

            byte[] raw_sha256 = Convert.FromBase64String("BU7ewdAhH2JP7Qy8qdT5QAsOSRxDdCryxbCr6/DJkNg=");

            Assert.AreEqual("Arteranos.Test.Structs.FPTest1", a_fp.Type);
            Assert.AreEqual(raw_sha256, a_fp.FPBytes);
            Assert.AreEqual("Arteranos.Test.Structs.FPTest1:BU7ewdAhH2JP7Qy8qdT5QAsOSRxDdCryxbCr6/DJkNg=", a_fp.ToString());
        }

        [Test]
        public void T002_Equality_Direct()
        {
            Fingerprint a_fp1 = new(a);
            Fingerprint a_fp2 = new(aa);

            Assert.AreEqual(a_fp1, a_fp2);
            Assert.AreEqual(a_fp1.GetHashCode(), a_fp2.GetHashCode());

            Fingerprint b_fp = new(b);

            Assert.AreNotEqual(a_fp1, b_fp);
            Assert.AreNotEqual(a_fp1.GetHashCode(), b_fp.GetHashCode());

            Fingerprint c_fp = new(c);

            Assert.AreNotEqual(a_fp1, c_fp);
            Assert.AreNotEqual(a_fp1.GetHashCode(), c_fp.GetHashCode());

            Assert.True(a_fp1 == a_fp2);
            Assert.False(a_fp1 != a_fp2);
            Assert.True(a_fp1 != b_fp);
            Assert.True(a_fp1 != c_fp);
        }

        [Test]
        public void T003_Equality_WithData()
        {
            Fingerprint empty = new();
            Fingerprint a_fp1 = new(a);
            Fingerprint b_fp = new(b);
            Fingerprint c_fp = new(c);

            Assert.False(a_fp1 == empty);

            Assert.True(a_fp1 == a);
            Assert.False(a_fp1 != a);
            Assert.True(a_fp1 == aa);
            Assert.True(a_fp1 != b);
            Assert.True(a_fp1 != c);

            Assert.True(a == a_fp1);
            Assert.False(a != a_fp1);
            Assert.True(aa == a_fp1);
            Assert.True(b_fp != a);
            Assert.True(c_fp != a);
        }

        [Test]
        public void T004_Serialize_Protobuf()
        {
            Fingerprint a_fp1 = new(a);

            byte[] proto = Convert.FromBase64String("Ch5BcnRlcmFub3MuVGVzdC5TdHJ1Y3RzLkZQVGVzdDESIAVO3sHQIR9iT+0MvKnU+UALDkkcQ3Qq8sWwq+vwyZDY");

            byte[] serialized = a_fp1.Serialize();

            // Debug.Log(Convert.ToBase64String(serialized));

            Assert.AreEqual(proto, serialized);
        }

        [Test]
        public void T005_Serialize_JSON()
        {
            Fingerprint a_fp1 = new(a);

            string json = "\"Ch5BcnRlcmFub3MuVGVzdC5TdHJ1Y3RzLkZQVGVzdDESIAVO3sHQIR9iT+0MvKnU+UALDkkcQ3Qq8sWwq+vwyZDY\"";

            string serialized = JsonConvert.SerializeObject(a_fp1);

            // Debug.Log(serialized);

            Assert.AreEqual(json, serialized);
        }

        [Test]
        public void T006_Deserialize()
        {
            Fingerprint a_fp1 = new(a);

            byte[] proto = Convert.FromBase64String("Ch5BcnRlcmFub3MuVGVzdC5TdHJ1Y3RzLkZQVGVzdDESIAVO3sHQIR9iT+0MvKnU+UALDkkcQ3Qq8sWwq+vwyZDY");
            string json = "\"Ch5BcnRlcmFub3MuVGVzdC5TdHJ1Y3RzLkZQVGVzdDESIAVO3sHQIR9iT+0MvKnU+UALDkkcQ3Qq8sWwq+vwyZDY\"";

            Fingerprint a_proto = Fingerprint.Deserialize(proto);

            Assert.AreEqual(a_fp1, a_proto);
            Assert.True(a == a_proto);

            Fingerprint a_json = JsonConvert.DeserializeObject<Fingerprint>(json);

            Assert.AreEqual(a_fp1, a_json);
            Assert.True(a == a_json);
        }
    }
}