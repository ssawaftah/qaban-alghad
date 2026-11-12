using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace WeighbridgeBridge
{
    public sealed class TransferFeedback
    {
        public string SendText { get; private set; }
        public string ServerText { get; private set; }
        public bool Written { get; private set; }
        public bool ServerError { get; private set; }
        public TransferFeedback()
        {
            Reset();
        }
        public void Reset()
        {
            Written = false;
            ServerError = false;
            SendText = "بانتظار الوزن";
            ServerText = "—";
        }
        public void Begin()
        {
            Written = false;
            if (SendText != "تم الإرسال")
                SendText = "جارٍ الإرسال";
        }
        public void BodyWritten(bool expectsReply = true)
        {
            Written = true;
            SendText = "تم الإرسال";
            ServerText = expectsReply ? "بانتظار الرد" : "—";
            ServerError = false;
        }
        public void Reply(int code, bool acknowledged, bool html, bool failure)
        {
            SendText = Written ? "تم الإرسال" : "لم يُؤكد الإرسال";
            ServerError = code < 200 || code >= 300 || html || failure;
            ServerText = ServerError    ? ("HTTP " + code +
                                           (html      ? " · HTML"
                                            : failure ? " · خطأ"
                                                      : ""))
                         : acknowledged ? "تم التحديث"
                                        : "HTTP " + code;
        }
        public void NoReply()
        {
            SendText = Written ? "تم الإرسال" : "لم يتم الاتصال";
            ServerText = "لم يصل رد";
            ServerError = true;
        }
    }
}
