using System.Numerics;

namespace GhostDevs.PluginEngine;

public interface IBlockchainPlugin : IDBAccessPlugin
{
    string[] ChainNames { get; }


    // This method is used by DataFetcher service.
    // Gets all needed blockchain data and put it into database.
    void Fetch();


    // Allows to verify message signatures for different chains.
    // Currently used by LockedUnlock endpoint.
    // 1) publicAddress - public address which signature we have to verify.
    // 2) messageBase16 - message which signature we verify, encoded in Base16.
    // 3) messagePrefixBase16 - prefix for message prepended to
    // message before signing, encoded in Base16. Optional, depends on chain implementation.
    // On Phantasma unlock requests are combined "random + request" and then signed.
    // 4) signatureBase16 - signature to verify, encoded in Base16.
    // 5) error - optional, method can return errors this way, or can just throw exceptions.
    // returns: true if signature is valid, false otherwise.
    public bool VerifySignatureAndOwnership(int chainId, string publicKey, string contractHash, string tokenId,
        string messageBase16, string messagePrefixBase16, string signatureBase16, out string error);


    public bool VerifySignature(string chainShortName, string publicKey, string messageBase16,
        string messagePrefixBase16, string signatureBase16, out string address, out string error);


    public BigInteger GetCurrentBlockHeight(string chain);
}
