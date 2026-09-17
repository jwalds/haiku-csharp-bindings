using System;

namespace Haiku.App
{
	/* Thrown whenever a shim call returns a nonzero hs_status. Haiku's own
	 * status_t error codes are meaningful (negative values from specific
	 * ranges mean specific things -- see Haiku's Errors.h), but v1 doesn't
	 * attempt to decode them into named .NET exception types yet; that's a
	 * natural follow-up once more of the API surface exists and it's clear
	 * which errors actually need distinguishing in managed code. */
	public class HaikuException : Exception
	{
		public int StatusCode { get; }

		public HaikuException(int statusCode)
			: base("Haiku API call failed with status " + statusCode)
		{
			StatusCode = statusCode;
		}
	}
}
