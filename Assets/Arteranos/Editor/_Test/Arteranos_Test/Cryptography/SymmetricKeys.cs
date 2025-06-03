using NUnit.Framework;

using Arteranos.Common.Cryptography;
using System.Text;
using System.Security.Cryptography;

namespace Arteranos.Test.Cryptography
{
    public class SymmetricKeys
    {
        [Test]
        public void Generate()
        {
            SymmetricKey key = SymmetricKey.Generate();

            Assert.IsNotNull(key);
            Assert.IsNotNull(key.IV);
        }

        [Test]
        public void Roundtrip()
        {
            byte[] data = Encoding.UTF8.GetBytes("this is to be encrypted");

            SymmetricKey key = SymmetricKey.Generate();
            byte[] iv = key.IV;
            key.Encrypt(data, out byte[] cipher);

            key.IV = iv;
            key.Decrypt(cipher, out byte[] returned);

            Assert.AreNotEqual(data, cipher);
            Assert.AreEqual(data, returned);
        }

        [Test]
        public void SharedSecret()
        {
            byte[] data = Encoding.UTF8.GetBytes("this is to be encrypted");

            System.Random random = new();

            byte[] secret = new byte[32];
            byte[] iv = new byte[16];

            random.NextBytes(secret);
            random.NextBytes(iv);

            SymmetricKey alice = SymmetricKey.Import(secret, iv);
            SymmetricKey bob = SymmetricKey.Import(secret, iv);

            alice.Encrypt(data, out byte[] cipher);
            bob.Decrypt(cipher, out byte[] returned);

            Assert.AreNotEqual(data, cipher);
            Assert.AreEqual(data, returned);
        }

        [Test]
        public void SharedSecret_Negative()
        {
            byte[] data = Encoding.UTF8.GetBytes("this is to be encrypted");

            System.Random random = new();

            byte[] secret = new byte[32];
            byte[] iv = new byte[16];
            byte[] wrongsecret = new byte[32];

            random.NextBytes(secret);
            random.NextBytes(iv);
            random.NextBytes(wrongsecret);

            SymmetricKey alice = SymmetricKey.Import(secret, iv);
            SymmetricKey bob = SymmetricKey.Import(wrongsecret, iv);

            alice.Encrypt(data, out byte[] cipher);

            byte[] returned = null;

            Assert.Throws<CryptographicException>(() =>
            {
                bob.Decrypt(cipher, out returned);
            });

            Assert.AreNotEqual(data, cipher);
            Assert.AreNotEqual(data, returned);
        }

        [Test]
        public void TransmissionScenario()
        {
            byte[] data = Encoding.UTF8.GetBytes("this is to be encrypted");

            System.Random random = new();

            // Bob broadcasts his session public key.
            AgreeKey bob = AgreeKey.Generate();
            bob.ExportPublicKey(out byte[] bobspk);

            // Alice broadcasts her session public key.
            AgreeKey alice = AgreeKey.Generate();
            alice.ExportPublicKey(out byte[] alicespk);

            byte[] iv = new byte[16];
            random.NextBytes(iv);

            // Alice generates the shared secret together with Bob's public key.
            alice.Agree(bobspk, out byte[] aliceShared);

            SymmetricKey alicekey = SymmetricKey.Import(aliceShared, iv);
            alicekey.Encrypt(data, out byte[] cipher);

            // Alice transmits the IV and the ciphertext. Her public key is already known.
            // ---------------------------------------------------------------------------

            // Bob generates the shared secret together with Alice's public key.
            bob.Agree(alicespk, out byte[] bobShared);

            SymmetricKey bobkey = SymmetricKey.Import(bobShared, iv);
            bobkey.Decrypt(cipher, out byte[] returned);

            // Bob should get the tranmitted message.
            Assert.AreNotEqual(data, cipher);
            Assert.AreEqual(data, returned);

            // Naturally, the shared secret has to be the same, else the decryption would fail.
            Assert.AreEqual(aliceShared, bobShared);
        }
    }
}