using NUnit.Framework;
using UnityEngine;

using Arteranos.Common.Cryptography;
using System.Text;

namespace Arteranos.Test.Cryptography
{
    public class AsymmetricKeys
    {
        [Test]
        public void Equality()
        {
            SignKey crypto = SignKey.Generate();
            crypto.ExportPrivateKey(out byte[] exported);
            SignKey clone = SignKey.ImportPrivateKey(exported);

            Assert.IsTrue(crypto == clone);
            Assert.IsFalse(crypto != clone);
            Assert.AreEqual(crypto.GetHashCode(), clone.GetHashCode());
            Assert.AreNotSame(crypto, clone); // But we're two copies of this one key.

            SignKey crypto2 = SignKey.Generate();

            Assert.IsTrue(crypto != crypto2);
            Assert.IsFalse(crypto == crypto2);
            Assert.AreNotEqual(crypto.GetHashCode(), crypto2.GetHashCode());
            Assert.AreNotSame(crypto, crypto2);
        }

        [Test]
        public void CreateSignKey()
        {
            SignKey crypto = SignKey.Generate();

            Assert.IsNotNull(crypto.KeyPair);
            Assert.IsNotNull(crypto.PublicKey);
        }

        [Test]
        public void ExportSignKey()
        {
            SignKey crypto = SignKey.Generate();

            crypto.ExportPrivateKey(out byte[] exported);
            Assert.IsNotNull(exported);
            Assert.IsTrue(exported.Length > 0);

            SignKey clone = SignKey.ImportPrivateKey(exported);

            Assert.AreEqual(crypto.PublicKey, clone.PublicKey);
        }

        [Test]
        public void SignVerify()
        {
            byte[] toSign = Encoding.UTF8.GetBytes("This is a text to be signed.");

            SignKey alice = SignKey.Generate();

            alice.Sign(toSign, out byte[] signature);
            alice.ExportPublicKey(out byte[] alicePubKey);

            // Bob gets Alice's Public Key and the document's signature.
            Assert.DoesNotThrow(() =>
            {
                AsymmetricKey.Verify(alicePubKey, toSign, signature);
            });
        }

        [Test]
        public void SignVerifyWithSerialized()
        {
            byte[] toSign = Encoding.UTF8.GetBytes("This is a text to be signed.");

            SignKey alice = SignKey.Generate();
            alice.ExportPublicKey(out byte[] alicePubKey);

            // Alice saves her session and restores it later.
            alice.ExportPrivateKey(out byte[] exported);
            SignKey clone = SignKey.ImportPrivateKey(exported);

            clone.Sign(toSign, out byte[] signature);

            // Bob gets Alice's Public Key and the document's signature.
            Assert.DoesNotThrow(() =>
            {
                AsymmetricKey.Verify(alicePubKey, toSign, signature);
            });
        }

        [Test]
        public void CreateEncryptionKey() 
        {
            EncryptionKey crypto = EncryptionKey.Generate();

            Assert.IsNotNull(crypto.KeyPair);
            Assert.IsNotNull(crypto.PublicKey);
        }

        [Test]
        public void ExportDecryptionKey() 
        {
            EncryptionKey crypto = EncryptionKey.Generate();

            crypto.ExportPrivateKey(out byte[] exported);
            Assert.IsNotNull(exported);
            Assert.IsTrue(exported.Length > 0);

            EncryptionKey clone = EncryptionKey.ImportPrivateKey(exported);

            Assert.AreEqual(crypto.PublicKey, clone.PublicKey);
        }

        [Test]
        public void EncryptDecrypt() 
        {
            byte[] toEncrypt = Encoding.UTF8.GetBytes("This is a text to be signed.");

            EncryptionKey bob = EncryptionKey.Generate();
            bob.ExportPublicKey(out byte[] exported);

            AsymmetricKey.Encrypt(exported, toEncrypt, out byte[] cipher);

            bob.Decrypt(cipher, out byte[] returned);

            Assert.AreEqual(toEncrypt, returned);
        }

        [Test]
        public void EncryptEncryptWithSerialized() 
        {
            byte[] toEncrypt = Encoding.UTF8.GetBytes("This is a text to be signed.");

            EncryptionKey bob = EncryptionKey.Generate();
            bob.ExportPublicKey(out byte[] exported);

            bob.ExportPrivateKey(out byte[] exportedkp);
            EncryptionKey clone = EncryptionKey.ImportPrivateKey(exportedkp);

            AsymmetricKey.Encrypt(exported, toEncrypt, out byte[] cipher);

            clone.Decrypt(cipher, out byte[] returned);

            Assert.AreEqual(toEncrypt, returned);
        }

        [Test]
        public void DoKeyAgreement()
        {
            AgreeKey alice = AgreeKey.Generate();
            AgreeKey bob = AgreeKey.Generate();

            alice.ExportPublicKey(out byte[] aliceExported);
            bob.ExportPublicKey(out byte[] bobExported);

            alice.Agree(bobExported, out byte[] aliceSecret);
            bob.Agree(aliceExported, out byte[] bobSecret);

            Assert.IsTrue(aliceSecret.Length > 0);
            Assert.AreEqual(aliceSecret, bobSecret);

            Debug.Log($"Shared Secret length: {aliceSecret.Length}");
        }

#if false
        /// <summary>
        /// libp2p's specs demand the standard Bitcoin encoding (BIP-340) for the public key,
        /// and requires the Schnorr signing algorithm, which are unsupported in BouncyCastle.
        /// </summary>
        [Test]
        [Ignore("Secp256k1 in libp2p uses the Bitcoin encoding")]
        public void SignVerifyWithAgreeKey()
        {
            byte[] toSign = Encoding.UTF8.GetBytes("This is a text to be signed.");

            AgreeKey alice = AgreeKey.Generate();

            alice.Sign(toSign, out byte[] signature);
            alice.ExportPublicKey(out byte[] alicePubKey);

            // Bob gets Alice's Public Key and the document's signature.
            Assert.DoesNotThrow(() =>
            {
                AsymmetricKey.Verify(alicePubKey, toSign, signature);
            });
        }
#endif
    }
}