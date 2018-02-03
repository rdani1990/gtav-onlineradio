using RadioExpansion.Core.WaveStreams;
using NAudio.Wave;
using System;
using System.Net;
using System.Threading;
using RadioExpansion.Core.Logging;
using System.Linq;
using RadioExpansion.Core.PlaylistReaders;
using System.Collections.Generic;
using RadioExpansion.Core.Serialization;

namespace RadioExpansion.Core.RadioPlayers
{
    [XmlWhitelistSerialization]
    public class OnlineRadio : Radio
    {
        private object _metaSyncLock = new object();
        private HttpWebResponse _response;
        private Uri _uri;

        private const int META_SYNC_INTERVAL = 15000;

        protected override int MetaDataSyncInterval => 15000;

        protected override bool AlwaysSleepWhenBufferIsFull => false;

        protected override void OnPathChanged()
        {
            IEnumerable<string> audioFiles, playlistFiles;
            GetFilesFromRadioFolder(out audioFiles, out playlistFiles);

            int playlistFilesCount = playlistFiles.Count();

            if (playlistFilesCount > 0)
            {
                string playlistPath = playlistFiles.First();
                var filesInPlaylist = PlaylistHelper.ProcessPlaylist(playlistPath);

                if (playlistFilesCount > 1)
                {
                    Logger.Log($"{playlistFilesCount} playlists were found in online radio folder '{RelativeDirectoryPath}'. Only one playlist is allowed! Took '{playlistPath}', ignored the rest.");
                }

                if (filesInPlaylist.Length == 0)
                {
                    Logger.Log($"Playlist '{playlistPath}' contains no elements.");
                }
                else if (filesInPlaylist[0].StartsWith("http"))
                {
                    _uri = new Uri(filesInPlaylist[0]);
                }
                else
                {
                    Logger.Log($"Unknown URL found on the first line of playlist file '{playlistPath}'. Url: {filesInPlaylist[0]}");
                }
            }
            else
            {
                Logger.Log($"No playlist was found for online radio folder '{RelativeDirectoryPath}'. Use playlist files with one of the following extensions: '.m3u', '.m3u8' or '.pls'.");
            }
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
                                Logger.LogTrack(Name, meta); // log new track title if radio is on
                            }
                            CurrentTrackMetaData = meta;
                        }
                    }
                    resp.Close();
                }
            }
            catch (Exception ex)
            {
                Logger.Log("Failed to refresh metadata info for radio '{0}' with URL '{1}'. Error: {2}", Name, _uri, ex);
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
