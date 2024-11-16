using Microsoft.AspNetCore.Mvc;
using PkmnFoundations.Support;
using System.Text;

namespace gtsCore.Controllers;

[ApiController]
public class PkvldtprodController : ControllerBase
{
    [HttpPost("pokemon/validate")]
    public async Task<IActionResult> ValidatePokemon()
    {
        byte[] requestData = new byte[(int)Request.Body.Length];
        Request.Body.Read(requestData, 0, (int)Request.Body.Length);

        // this is a mysterious token of unknown purpose. It seems to vary
        // with the type of request being done.
        // On GTS requests, it's 83 characters long and begins with NDS.
        // In Random Matchup, it looks more like a base64 string, is 88
        // chars long and encodes 64 bytes of random looking data.
        // It is null terminated (variable length), followed immediately 
        // by the rest of the message.
        int tokenLength = Array.IndexOf<byte>(requestData, 0x00);
        String token = StringHelper.BytesToString(requestData, 0, tokenLength, Encoding.UTF8);
        int offset = tokenLength + 1;
            
        RequestType type = (RequestType)BitConverter.ToInt16(requestData, offset);
        offset += 2;

        PokemonValidationResult[] results;

        switch (type)
        {
            case RequestType.RandomMatchup:
            case RequestType.GTS:
                {
                    int pkmCount = (requestData.Length - offset) / 220;
                    results = new PokemonValidationResult[pkmCount];

                    for (int x = 0; x < results.Length; x++)
                    {
                        byte[] data = new byte[220];
                        Array.Copy(requestData, offset + x, data, 0, 220);
                        Pokemon5 pkm = new Pokemon5(data);
                        // todo: actual validation goes here
                        results[x] = PokemonValidationResult.Valid;
                    }
                } break;
            /*
            case RequestType.BattleSubway:
                {
                    // todo: Need more info on this structure
                    // todo: Perform actual validation here so that we can
                    // error out before the player actually does their challenge
                } break;
            */
            // todo: there also appears to be a Battle Video request?
            default:
                {
                    // Don't understand this request. Give it a response containing
                    // all 00s so stuff depending on it won't break.
                    // The game accepts a hash of all 00s and doesn't care what's contained
                    // in the response beyond its expected length so this will work.
                    // fixme: Once we start generating real signatures, they will prevent
                    // this from returning the necessary number of 00s in all cases.
                    results = [
                        PokemonValidationResult.Valid,
                        PokemonValidationResult.Valid,
                        PokemonValidationResult.Valid,
                        PokemonValidationResult.Valid,
                        PokemonValidationResult.Valid,
                        PokemonValidationResult.Valid];
                } break;
        }

        PartyValidationResult result = PartyValidationResult.Valid;
        foreach (PokemonValidationResult pkr in results)
        {
            if (pkr != PokemonValidationResult.Valid) result = PartyValidationResult.Invalid;
        }

        Response.ContentType = "text/plain";
        Response.Body.WriteByte((byte)result); // success
        foreach (PokemonValidationResult pkr in results)
        {
            Response.Body.Write(BitConverter.GetBytes((int)pkr), 0, 4);
        }
        // placeholder for signature.
        // Should be 128 bytes of an unknown hashing/signing algorithm
        await Response.WriteAsync("Hey this is a totally legit pkvldtprod signature pwease accept it uwu");
        Response.Body.Write(new byte[69], 0, 69); // nice
        //Response.Body.Write(new byte[128], 0, 128);

        return Ok();
    }

    private enum PartyValidationResult : byte
    {
        Valid = 0x00,
        Invalid = 0x01
    }

    private enum PokemonValidationResult : int
    {
        Valid = 0x00000000,
        Invalid = 0x3c000000
    }

    private enum RequestType : short
    {
        RandomMatchup = 0x0000,
        GTS = 0x0100,
        BattleSubway = 0x0400
    }
}

internal class Pokemon5
{
    // placeholder until I actually do this for real...
    private byte[] _data;
    public Pokemon5(byte[] data)
    {
        if (data.Length != 220) throw new ArgumentException();
        _data = data;
    }
}
