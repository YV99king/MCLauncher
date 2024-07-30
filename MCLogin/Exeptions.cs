using System;
using System.Runtime.Serialization;

namespace MCLauncher;

/// <summary>
/// The exception that is thrown when someone trys to login to microsoft with wrong username or password
/// </summary>
[Serializable]
public class MicrosoftLoginWrongCredentialsException : ArgumentException
{
	/// <summary>
	/// Creates a new <see cref="MicrosoftLoginWrongCredentialsException"/> with it's message set to empty string
	/// </summary>
	public MicrosoftLoginWrongCredentialsException() { }
    /// <summary>
    /// Creates a new <see cref="MicrosoftLoginWrongCredentialsException"/> with it's message set to <paramref name="message"/>
    /// </summary>
    /// <param name="message">the exception's message</param>
    public MicrosoftLoginWrongCredentialsException(string message) : base(message) { }
    /// <summary>
    /// Creates a new <see cref="MicrosoftLoginWrongCredentialsException"/> with it's message set to <paramref name="message"/> and it's inner exception <paramref name="inner"/>
    /// </summary>
    /// <param name="message">the exception's message</param>
    /// <param name="inner">the exception's inner exception</param>
    public MicrosoftLoginWrongCredentialsException(string message, Exception inner) : base(message, inner) { }

    public MicrosoftLoginWrongCredentialsException(string message, string paramName) : base(message, paramName) { }
    public MicrosoftLoginWrongCredentialsException(string message, string paramName, Exception innerException) : base(message, paramName, innerException) { }
    protected MicrosoftLoginWrongCredentialsException(
	  SerializationInfo info,
	  StreamingContext context) : base(info, context) { }
}

[Serializable]
public class XboxLiveException : Exception
{
	public XboxLiveException(uint exceptionCode)
	{
		this.ExceptionCode = exceptionCode;
	}
	public XboxLiveException(uint exceptionCode, string message) : base(message)
	{
        this.ExceptionCode = exceptionCode;
    }
    public XboxLiveException(uint exceptionCode, string message, Exception inner) : base(message, inner)
	{
        this.ExceptionCode = exceptionCode;
    }
    protected XboxLiveException(
	  SerializationInfo info,
	  StreamingContext context) : base(info, context)
	{
		this.ExceptionCode = info.GetUInt32("ExceptionCode");
	}

    public uint ExceptionCode { get; }
}
[Serializable]
public class XboxLiveLoginFailedException : XboxLiveException
{
	public XboxLiveLoginFailedException() : base(0, "Xbox Live login failed") { }
	public XboxLiveLoginFailedException(string message) : base(0, message) { }
	public XboxLiveLoginFailedException(string message, Exception inner) : base(0, message, inner) { }
    public XboxLiveLoginFailedException(uint exceptionCode) : base(exceptionCode, "Xbox Live login failed") { }
    public XboxLiveLoginFailedException(uint exceptionCode, string message) : base(exceptionCode, message) { }
    public XboxLiveLoginFailedException(uint exceptionCode, string message, Exception inner) : base(exceptionCode, message, inner) { }
    protected XboxLiveLoginFailedException(
	  SerializationInfo info,
	  StreamingContext context) : base(info, context) { }
}
/// <summary>
/// 
/// </summary>
[Serializable]
public class XboxLiveAccountTooYoungException : XboxLiveException
{
	public XboxLiveAccountTooYoungException() : base(2148916238, "account belongs to someone under 18 and needs to be added to a family (2148916238)") { }
	public XboxLiveAccountTooYoungException(string message) : base(2148916238, message) { }
	public XboxLiveAccountTooYoungException(string message, Exception inner) : base(2148916238, message, inner) { }
	protected XboxLiveAccountTooYoungException(
	  SerializationInfo info,
	  StreamingContext context) : base(info, context) { }
}