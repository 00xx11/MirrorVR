// Influenced by: https://github.com/adrenak/univoice-unimic-input/blob/master/Assets/Adrenak.UniVoice.UniMicInput/Runtime/UniVoiceUniMicInput.cs

namespace Assets.Metater.MetaVoiceChat.Input.Mic
{
    public class VcMicAudioInput : VcAudioInput
    {
        public VcMic mic;

        public override void StartLocalPlayer()
        {
            int samplesPerFrame = metaVc.config.samplesPerFrame;

            mic = new(this, samplesPerFrame);
            mic.OnFrameReady += SendAndFilterFrame;
            mic.StartRecording();
        }

        private void OnDestroy()
        {
            if (mic == null)
            {
                return;
            }

            mic.OnFrameReady -= SendAndFilterFrame;
            mic.Dispose();
        }
    }
}