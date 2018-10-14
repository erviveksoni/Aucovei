using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources.Core;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.SpeechSynthesis;

namespace Aucovei.Device.Services
{
    /// <summary>
    /// The central authority on playback in the application
    /// providing access to the player and active playlist.
    /// </summary>
    public class PlaybackService
    {
        private readonly MediaPlayer player;
        private SpeechSynthesizer synthesizer;
        private ResourceContext speechContext;
        private ResourceMap speechResourceMap;
        private SoundFiles currentSource;

        public enum SoundFiles
        {
            Default,
            Horn,
            CensorBeep,
            BootUp,
            Disconnected
        }

        public PlaybackService()
        {
            // Create the player instance
            this.player = new MediaPlayer { AutoPlay = false };
            this.player.Volume = 0.5;

            this.synthesizer = new SpeechSynthesizer();
            this.speechContext = ResourceContext.GetForCurrentView();
            this.speechContext.Languages = new string[] { SpeechSynthesizer.DefaultVoice.Language };
            this.synthesizer.Voice = SpeechSynthesizer.AllVoices[1];
        }

        public void PlaySoundFromFile(SoundFiles file, bool loop = false)
        {
            this.currentSource = file;
            this.player.Pause();
            this.player.IsLoopingEnabled = loop;
            this.player.Source = MediaSource.CreateFromUri(new Uri($"ms-appx:///Assets/{this.currentSource.ToString()}.mp3",
                UriKind.RelativeOrAbsolute));
            this.player.Play();
        }

        public void StopSoundPlay()
        {
            this.player.Pause();
            this.player.IsLoopingEnabled = false;
            this.player.Source = null;
        }

        public void ChangeMediaSource(SoundFiles file)
        {
            if (this.currentSource != file)
            {
                this.currentSource = file;
                this.player.Pause();
                this.player.Source = MediaSource.CreateFromUri(new Uri($"ms-appx:///Assets/{this.currentSource.ToString()}.mp3",
                    UriKind.RelativeOrAbsolute));
            }
        }

        public void PlaySound()
        {
            this.player.Play();
        }

        public async Task SynthesizeTextAsync(string text)
        {
            // Create a stream from the text. This will be played using a media element.
            var audio = await this.synthesizer.SynthesizeTextToStreamAsync(text);
            this.player.Source = MediaSource.CreateFromStream(audio, audio.ContentType);
            this.player.Play();
        }
    }
}
