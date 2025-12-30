using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using PhantasmaPhoenix.Cryptography;

namespace Backend.Service.Api.Controllers.V1;

public enum VerifyMessageInputFormat
{
    Plain,
    Base16,
    Base64
}

public class VerifyMessageController : BaseControllerV1
{
    [HttpGet("verifyMessage")]
    [ApiInfo(typeof(bool), "Verifies if message is signed by specified address", cacheDuration: 60, cacheTag: "verifyMessage")]
    public Task<bool> GetResults(
        [FromQuery] string message,
        [FromQuery] VerifyMessageInputFormat messageFormat,
        [FromQuery] string signature,
        [FromQuery] VerifyMessageInputFormat signatureFormat,
        [FromQuery] string signerAddress,
        [FromQuery] SignatureKind signatureKind = SignatureKind.Ed25519,
        [FromQuery] ECDsaCurve ecdsaCurve = ECDsaCurve.Secp256k1

    )
    {
        try
        {
            byte[] messageBytes = messageFormat switch
            {
                VerifyMessageInputFormat.Plain => Encoding.UTF8.GetBytes(message),
                VerifyMessageInputFormat.Base16 => Base16.Decode(message),
                VerifyMessageInputFormat.Base64 => Convert.FromBase64String(message),
                _ => throw new("Message not provided or format is not supported")
            };

            byte[] signatureBytes = signatureFormat switch
            {
                VerifyMessageInputFormat.Base16 => Base16.Decode(signature),
                VerifyMessageInputFormat.Base64 => Convert.FromBase64String(signature),
                _ => throw new("Signature not provided or format is not supported")
            };

            var signer = PhantasmaPhoenix.Cryptography.Address.Parse(signerAddress);

            var result = signatureKind switch
            {
                SignatureKind.Ed25519 => Ed25519.Verify(signatureBytes, messageBytes, signer.GetPublicKey()),
                SignatureKind.ECDSA => ECDsa.Verify(signatureBytes, messageBytes, signer.GetPublicKey(), ecdsaCurve),
                _ => throw new("Signature kind not provided")
            };

            return Task.FromResult(result);
        }
        catch (Exception e)
        {
            throw new ApiUnexpectedException($"Exception caught during signature verification: {e.Message}", e);
        }
    }
}
