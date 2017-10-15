using GTAV_OnlineRadio.AsiLibrary.WaveStreams;
using NAudio.Wave;
using System;
using System.Net;
using System.Threading;
using System.Xml.Linq;

namespace GTAV_OnlineRadio.AsiLibrary.RadioPlayers
{
    public class OnlineRadio : Radio
    {
        private object _metaSyncLock = new object();
        private HttpWebResponse _response;
        private Uri _uri;

        private const int META_SYNC_INTERVAL = 15000;

        protected override bool AlwaysSleepWhenBufferIsFull => false;

        //public OnlineRadio(string name, Uri uri, float volume) : base(name, uri, volume, META_SYNC_INTERVAL) { }

        public OnlineRadio(string folder, Uri uri, XElement config) : base(folder, config, META_SYNC_INTERVAL)
        {
            _uri = uri;
        }

        public override void RefreshMetaInfo()
        {
            if (!Monitor.TryEnter(_metaSyncLock)) // if the previous MetaSyncTimer_Elapsed is still running, just skip this round
            {
                return;
            }
            
            try
            {
                var webRequest = (HttpWebRequest)WebRequest.Create(_uri);
                webRequest.Headers["Icy-MetaData"] = "1"; // asks the streamer to provide metadata
                var resp = (HttpWebResponse)webRequest.GetResponse();
                string icyMetaInt = resp.Headers["Icy-MetaInt"]; // every Icy-MetaInt-th byte starts with the metadata

                if (!String.IsNullOrEmpty(icyMetaInt))
                {
                    using (var responseStream = resp.GetResponseStream())
                    {
                        int bytesToSkip = Int32.Parse(icyMetaInt);
                        int bytesRead = 0;
                        var tmpBuffer = new byte[bytesToSkip];

                        // if e. g. icyMetaInt == 8192, it means that the first 8192 bytes are music, so we have to drop those, and read meta after that
                        while ((bytesRead += responseStream.Read(tmpBuffer, bytesRead, bytesToSkip - bytesRead)) < tmpBuffer.Length);

                        var meta = MetaHelper.ReadMetaData(responseStream);
                        if (meta != null && meta.StreamTitle != ((OnlineStreamMetaData)CurrentTrackMetaData)?.StreamTitle)
                        {
                            if (IsPlaying && LOG_EVERY_PLAYED_TRACK)
                            {
                                Logger.Instance.LogTrack(Name, meta); // log new track title if radio is on
                            }
                            CurrentTrackMetaData = meta;
                        }
                    }
                    resp.Close();
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Log("error {0} {1}", _uri, ex);
            }
            finally
            {
                Monitor.Exit(_metaSyncLock); // release the lock
            }
        }

        protected override void StreamAudio()
        {
            var webRequest = (HttpWebRequest)WebRequest.Create(_uri);
            webRequest.Headers["Icy-MetaData"] = "1"; // asks the streamer to provide metadata

            try
            {
                _response = (HttpWebResponse)webRequest.GetResponse();
            }
            catch (WebException e)
            {
                if (e.Status != WebExceptionStatus.RequestCanceled)
                {
                    Console.Error.WriteLine(e.Message);
                }
                return;
            }

            string metaInt = _response.Headers["Icy-MetaInt"]; // every Icy-MetaInt-th byte starts with the metadata
            var bufferedStream = new BufferedShoutcastStream(_response.GetResponseStream(), String.IsNullOrEmpty(metaInt) ? null : (int?)Int32.Parse(metaInt));
            bufferedStream.MetaDataReceived += BufferedStream_MetaDataReceived;

            var firstFrame = Mp3Frame.LoadFromStream(bufferedStream);

            StreamAudio(new ShoutcastWaveStream(bufferedStream, new Mp3WaveFormat(firstFrame.SampleRate, firstFrame.ChannelMode == ChannelMode.Mono ? 1 : 2, firstFrame.FrameLength, firstFrame.BitRate)));
        }

        private void BufferedStream_MetaDataReceived(object sender, MetaDataReceivedEventArgs e)
        {
            CurrentTrackMetaData = e.MetaData;
        }

        protected override void StreamingFinished()
        {
            _response?.Close();
        }

    }
}
