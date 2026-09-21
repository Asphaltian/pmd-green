using SDL;
using static SDL.SDL3;

namespace PMDGreen;

internal sealed unsafe class AudioOutput : IDisposable
{
    private const int Channels = 2;
    private const int TargetQueuedMilliseconds = 60;
    private const int AveragedMilliseconds = 1000;
    private const float MaxSpeedChange = 0.005f;

    private readonly Lock _lock = new();
    private SDL_AudioStream* _stream;
    private int _sampleRate;
    private float _averageQueued;

    public AudioOutput(int volume)
    {
        if (!SDL_InitSubSystem(SDL_InitFlags.SDL_INIT_AUDIO))
        {
            throw new InvalidOperationException($"Could not initialize audio: {SDL_GetError()}");
        }

        _stream = SDL_OpenAudioDeviceStream(SDL_AUDIO_DEVICE_DEFAULT_PLAYBACK, null, null, 0);
        if (_stream is null)
        {
            throw new InvalidOperationException($"Could not open an audio device: {SDL_GetError()}");
        }

        SetVolume(volume);
        SDL_ResumeAudioStreamDevice(_stream);
    }

    public void SetVolume(int volume)
    {
        lock (_lock)
        {
            SDL_SetAudioStreamGain(_stream, Math.Clamp(volume, 0, 100) / 100f);
        }
    }

    public void Play(ReadOnlySpan<short> samples, int sampleRate)
    {
        lock (_lock)
        {
            if (_stream is not null)
            {
                Queue(samples, sampleRate);
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            SDL_DestroyAudioStream(_stream);
            _stream = null;
        }
    }

    private void Queue(ReadOnlySpan<short> samples, int sampleRate)
    {
        int bytesPerSecond = sampleRate * Channels * sizeof(short);
        int target = bytesPerSecond * TargetQueuedMilliseconds / 1000;
        if (sampleRate != _sampleRate)
        {
            var format = new SDL_AudioSpec { format = SDL_AudioFormat.SDL_AUDIO_S16LE, channels = Channels, freq = sampleRate };
            SDL_SetAudioStreamFormat(_stream, &format, null);
            _sampleRate = sampleRate;
            _averageQueued = target;
        }

        int queued = SDL_GetAudioStreamQueued(_stream);
        if (queued == 0)
        {
            fixed (byte* silence = new byte[target])
            {
                SDL_PutAudioStreamData(_stream, (nint)silence, target);
            }

            (queued, _averageQueued) = (target, target);
        }

        int bytes = samples.Length * sizeof(short);
        float weight = Math.Min(bytes * 1000f / bytesPerSecond / AveragedMilliseconds, 1);
        _averageQueued += (queued - _averageQueued) * weight;
        float error = Math.Clamp((_averageQueued - target) / target, -1, 1);
        SDL_SetAudioStreamFrequencyRatio(_stream, 1 + (error * MaxSpeedChange));

        fixed (short* data = samples)
        {
            SDL_PutAudioStreamData(_stream, (nint)data, bytes);
        }
    }
}
