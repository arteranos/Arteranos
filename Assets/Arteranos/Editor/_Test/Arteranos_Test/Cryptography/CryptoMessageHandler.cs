using NUnit.Framework;

using Arteranos.Common.Cryptography;
using System.Text;
using System.Security.Cryptography;
using Ipfs.Cryptography.Proto;

namespace Arteranos.Test.Cryptography
{
    public class CryptoMessageHandlers
    {
        [Test]
        public void Construct()
        {
            ISignKey signKey = SignKey.Generate();

            CryptoMessageHandler cmh = new(signKey);

            Assert.IsNotNull(cmh);
            Assert.IsNotNull(cmh.SignPublicKey);
            Assert.IsNotNull(cmh.AgreePublicKey);

            Assert.AreEqual(cmh.SignPublicKey, signKey.PublicKey);
            Assert.AreNotEqual(cmh.SignPublicKey, cmh.AgreePublicKey);

            CryptoMessageHandler cmh2 = new(signKey);

            Assert.IsNotNull(cmh2);
            Assert.AreEqual(cmh.SignPublicKey, cmh2.SignPublicKey);
            Assert.AreNotEqual(cmh.AgreePublicKey, cmh2.AgreePublicKey);
        }

        [Test]
        public void TransmitUnencrypted()
        {
            ISignKey signKey = SignKey.Generate();
            CryptoMessageHandler cmh = new(signKey);

            byte[] message = Encoding.UTF8.GetBytes("this is to be unencrypted and signed");

            cmh.TransmitMessage(message, new PublicKey[0], out CMSPacket messageData);

            Assert.IsNotNull(messageData);
        }

        [Test]
        public void TransmitEncrypted()
        {
            ISignKey signKey = SignKey.Generate();
            CryptoMessageHandler cmh = new(signKey);

            AgreeKey receiver = AgreeKey.Generate();

            byte[] message = Encoding.UTF8.GetBytes("this is to be encrypted and signed");

            cmh.TransmitMessage(message, receiver.PublicKey, out CMSPacket messageData);

            Assert.IsNotNull(messageData);
        }

        [Test]
        public void TransmitAndReceive()
        {
            ISignKey aliceSignKey = SignKey.Generate();
            CryptoMessageHandler aliceCmh = new(aliceSignKey);

            ISignKey bobSignKey = SignKey.Generate();
            CryptoMessageHandler bobCmh = new(bobSignKey);

            byte[] message = Encoding.UTF8.GetBytes("this is to be encrypted and signed");

            aliceCmh.TransmitMessage(message, bobCmh.AgreePublicKey, out CMSPacket messageData);

            bobCmh.ReceiveMessage(messageData, out byte[] decodedMessage, out PublicKey supposedSender);

            Assert.AreEqual(message, decodedMessage);
            Assert.AreEqual(supposedSender, aliceCmh.SignPublicKey);
        }

        [Test]
        public void TransmitAndReceiveSignOnly()
        {
            ISignKey aliceSignKey = SignKey.Generate();
            CryptoMessageHandler aliceCmh = new(aliceSignKey);

            ISignKey bobSignKey = SignKey.Generate();
            CryptoMessageHandler bobCmh = new(bobSignKey);

            byte[] message = Encoding.UTF8.GetBytes("this is to be encrypted and signed");

            aliceCmh.TransmitMessage(message, new PublicKey[0], out CMSPacket messageData);

            bobCmh.ReceiveMessage(messageData, out byte[] decodedMessage, out PublicKey supposedSender);

            Assert.AreEqual(message, decodedMessage);
            Assert.AreEqual(supposedSender, aliceCmh.SignPublicKey);
        }

        [Test]
        public void AttemptEavesdrop()
        {
            ISignKey aliceSignKey = SignKey.Generate();
            CryptoMessageHandler aliceCmh = new(aliceSignKey);

            ISignKey bobSignKey = SignKey.Generate();
            CryptoMessageHandler bobCmh = new(bobSignKey);

            ISignKey EveSignKey = SignKey.Generate();
            CryptoMessageHandler EveCmh = new(EveSignKey);

            byte[] message = Encoding.UTF8.GetBytes("this is to be encrypted and signed");

            aliceCmh.TransmitMessage(message, bobCmh.AgreePublicKey, out CMSPacket messageData);

            Assert.Throws<CryptographicException>(() => 
            {
                EveCmh.ReceiveMessage(messageData, out byte[] decodedMessage, out PublicKey supposedSender);
            });
        }

        [Test]
        public void AttemptMaliciousSender()
        {
            ISignKey aliceSignKey = SignKey.Generate();
            CryptoMessageHandler aliceCmh = new(aliceSignKey);

            ISignKey bobSignKey = SignKey.Generate();
            CryptoMessageHandler bobCmh = new(bobSignKey);

            ISignKey MallorySignKey = SignKey.Generate();
            CryptoMessageHandler MalloryCmh = new(MallorySignKey);

            byte[] message = Encoding.UTF8.GetBytes("this is to be encrypted and signed");

            MalloryCmh.TransmitMessage(message, bobCmh.AgreePublicKey, out CMSPacket messageData);

            bobCmh.ReceiveMessage(messageData, out byte[] decodedMessage, out PublicKey supposedSender);

            Assert.AreEqual(message, decodedMessage);
            Assert.AreNotEqual(aliceCmh.SignPublicKey, supposedSender);
            Assert.AreEqual(MalloryCmh.SignPublicKey, supposedSender);
        }
    }
}
