using System;
using System.Runtime.Serialization;

namespace MCLauncher;

/// <summary>
/// The exception that is thrown when someone tries to login to Microsoft with wrong username or password
/// </summary>
[Serializable]
public class MicrosoftLoginWrongCredentialsException : ArgumentException //TODO: finish error system
{
	/// <summary>
	/// Creates a new <see cref="MicrosoftLoginWrongCredentialsException"/> exception with it's message set to empty string
	/// </summary>
	public MicrosoftLoginWrongCredentialsException() { }
    /// <summary>
    /// Creates a new <see cref="MicrosoftLoginWrongCredentialsException"/> exception with it's message set to <paramref name="message"/>
    /// </summary>
    /// <param name="message">the exception's message</param>
    public MicrosoftLoginWrongCredentialsException(string message) : base(message) { }
    /// <summary>
    /// Creates a new <see cref="MicrosoftLoginWrongCredentialsException"/> exception with it's message set to <paramref name="message"/> and it's inner exception <paramref name="inner"/>
    /// </summary>
    /// <param name="message">the exception's message</param>
    /// <param name="inner">the exception's inner exception</param>
    public MicrosoftLoginWrongCredentialsException(string message, Exception inner) : base(message, inner) { }
    /// <summary>
    /// Initialize a new <see cref="MicrosoftLoginWrongCredentialsException"/> exception with it's message set to <paramref name="message"/> and it's 
    /// </summary>
    /// <param name="message"></param>
    /// <param name="paramName"></param>
    public MicrosoftLoginWrongCredentialsException(string message, string paramName) : base(message, paramName) { }
    public MicrosoftLoginWrongCredentialsException(string message, string paramName, Exception innerException) : base(message, paramName, innerException) { }
    protected MicrosoftLoginWrongCredentialsException(
	  SerializationInfo info,
#pragma warning disable SYSLIB0051 // Type or member is obsolete
      StreamingContext context) : base(info, context) { }
#pragma warning restore SYSLIB0051 // Type or member is obsolete
}

/// <summary>
/// The exception that is thrown when an Xbox live action fails
/// </summary>
[Serializable]
public class XboxLiveException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="XboxLiveException"/> class with a specified error code
    /// </summary>
    /// <param name="exceptionCode">The exception's error code</param>
    public XboxLiveException(uint exceptionCode) =>
        this.ExceptionCode = exceptionCode;
    /// <summary>
    /// Initializes a new instance of the <see cref="XboxLiveException"/> class with a specified error code and a specified message
    /// </summary>
    /// <param name="exceptionCode">The exception's error code</param>
    /// <param name="message">The message that describes the error</param>
    public XboxLiveException(uint exceptionCode, string message) : base(message) =>
        this.ExceptionCode = exceptionCode;
    /// <summary>
    /// Initializes a new instance of the <see cref="XboxLiveException"/> class with a specified error code, a specified message and a reference inner exception
    /// </summary>
    /// <param name="exceptionCode"></param>
    /// <param name="message"></param>
    /// <param name="inner"></param>
    public XboxLiveException(uint exceptionCode, string message, Exception inner) : base(message, inner) =>
        this.ExceptionCode = exceptionCode;
    protected XboxLiveException(
	  SerializationInfo info,
#pragma warning disable SYSLIB0051 // Type or member is obsolete
      StreamingContext context) : base(info, context) =>
		this.ExceptionCode = info.GetUInt32("ExceptionCode");
#pragma warning restore SYSLIB0051 // Type or member is obsolete

    public uint ExceptionCode { get; }
}
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