using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TuneDetector.Audio;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Media;
using Windows.Media.Audio;
using Windows.Media.Capture;
using Windows.Media.Devices;
using Windows.Media.MediaProperties;
using Windows.Media.Render;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace TuneDetector
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    /// <remarks>
    ///  References:
    ///   https://mtaulty.com/2016/02/09/windows-10-uwp-audiographrecording-microphone-to-wav-file/
    ///   https://docs.microsoft.com/en-us/windows/uwp/audio-video-camera/audio-graphs
    ///   
    /// </remarks>
    public sealed partial class MainPage : Page
    {
        AudioGraph _graph;
        AudioFileOutputNode _outputNode;
        private AudioFrameOutputNode _frameOutputNode;

        public MainPage()
        {
            InitializeComponent();

            BtnStart.Click += OnStart;
            BtnStop.Click += OnStop;
        }

        private async void OnStart(object sender, RoutedEventArgs e)
        {
            var file = await PickFileAsync();

            if (file != null)
            {
                var result = await AudioGraph.CreateAsync(
                new AudioGraphSettings(AudioRenderCategory.Media));

                if (result.Status == AudioGraphCreationStatus.Success)
                {
                    _graph = result.Graph;

                    var microphone = await DeviceInformation.CreateFromIdAsync(
                    MediaDevice.GetDefaultAudioCaptureId(AudioDeviceRole.Default));

                    // In my scenario I want 16K sampled, mono, 16-bit output
                    var outProfile = MediaEncodingProfile.CreateWav(AudioEncodingQuality.Low);
                    outProfile.Audio = AudioEncodingProperties.CreatePcm(16000, 1, 16);

                    var outputResult = await _graph.CreateFileOutputNodeAsync(file, outProfile);

                    if (outputResult.Status == AudioFileNodeCreationStatus.Success)
                    {
                        _outputNode = outputResult.FileOutputNode;

                        var inProfile = MediaEncodingProfile.CreateWav(AudioEncodingQuality.High);

                        var inputResult = await _graph.CreateDeviceInputNodeAsync(MediaCategory.Communications);

                        if (inputResult.Status == AudioDeviceNodeCreationStatus.Success)
                        {
                            inputResult.DeviceInputNode.AddOutgoingConnection(_outputNode);

                            _frameOutputNode = _graph.CreateFrameOutputNode();
                            _graph.QuantumStarted += AudioGraphOnQuantumStarted;

                            _graph.Start();
                        }
                    }
                }
            }
        }

        private async void OnStop(object sender, RoutedEventArgs e)
        {
            if (_graph != null)
            {
                _graph?.Stop();

                await _outputNode.FinalizeAsync();

                // assuming that disposing the graph gets rid of the input/output nodes?
                _graph?.Dispose();

                _graph = null;
            }
        }

        private void AudioGraphOnQuantumStarted(AudioGraph sender, object args)
        {
            var frame = _frameOutputNode.GetFrame();
            ProcessFrameOutput(frame);
        }

        private unsafe void ProcessFrameOutput(AudioFrame frame)
        {
            using (AudioBuffer buffer = frame.LockBuffer(AudioBufferAccessMode.Write))
            using (var reference = buffer.CreateReference())
            {
                // Get the buffer from the AudioFrame
                ((IMemoryBufferByteAccess)reference).GetBuffer(out byte* dataInBytes, out uint capacityInBytes);

                var dataInFloat = (float*)dataInBytes;
            }

        }

        private async Task<StorageFile> PickFileAsync()
        {
            FileSavePicker picker = new FileSavePicker();
            picker.FileTypeChoices.Add("Wave File (PCM)", new List<string> { ".wav" });
            picker.SuggestedStartLocation = PickerLocationId.Desktop;

            var file = await picker.PickSaveFileAsync();

            return (file);
        }
    }
}
