using System;
using UnityEngine;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.IO;

class DSPCaptureAndWrite : MonoBehaviour
{
    [SerializeField]
    private string mFilePath;
    [SerializeField]
    private string mBusPath;

    private FMOD.DSP_READ_CALLBACK mReadCallback;
    private GCHandle mObjHandle;
    private FMOD.DSP_DESCRIPTION mDSPDescription;

    private FMOD.DSP mDSP;
    private FMOD.ChannelGroup mCG;
    private List<float> mAudioData;

    private int mSampleRate;
    private int mNumChannels;
    private int mBitDepth = 32; // Assumes data will be 32 bit PCM Float, which it will automatically be in the FMOD Mixer graph

    private bool mDSPsCreated;
    private bool mRecording;
    private FileStream fs;
    private BinaryWriter bw;


    [AOT.MonoPInvokeCallback(typeof(FMOD.DSP_READ_CALLBACK))]
    static FMOD.RESULT CaptureDSPReadCallback(ref FMOD.DSP_STATE dsp_state, IntPtr inbuffer, IntPtr outbuffer, uint length, int inchannels, ref int outchannels)
    {
        // Copy the input buffer to an intermediate buffer
        int lengthElements = (int)length * inchannels;
        float[] data = new float[lengthElements];
        Marshal.Copy(inbuffer, data, 0, lengthElements);

        // Get instance of DSPCaptureAndWrite from user data assigned to DSP
        FMOD.DSP_STATE_FUNCTIONS functions = (FMOD.DSP_STATE_FUNCTIONS)Marshal.PtrToStructure(dsp_state.functions, typeof(FMOD.DSP_STATE_FUNCTIONS));
        IntPtr userData;
        functions.getuserdata(ref dsp_state, out userData);
        if (userData != IntPtr.Zero)
        {
            GCHandle objHandle = GCHandle.FromIntPtr(userData);
            DSPCaptureAndWrite obj = objHandle.Target as DSPCaptureAndWrite;

            // If currently recording, add buffer contents to recorded audio data
            if (obj.mRecording)
            {
                obj.mNumChannels = inchannels;
                obj.mAudioData.AddRange(data);
            }
        }

        // Copy the input buffer to the output buffer so we can still hear it
        Marshal.Copy(data, 0, outbuffer, lengthElements);

        return FMOD.RESULT.OK;
    }

    void Start()
    {
        // Assign the DSP capture callback to a member variable to avoid garbage collection
        mReadCallback = CaptureDSPReadCallback;

        // Get DSP/Audio info and initialize list used to to store capture audio
        uint bufferLength;
        FMODUnity.RuntimeManager.CoreSystem.getDSPBufferSize(out _, out _);
        FMODUnity.RuntimeManager.CoreSystem.getSoftwareFormat(out mSampleRate, out _, out _);
        mAudioData = new();

        // Get a handle to this object to pass into the callback
        mObjHandle = GCHandle.Alloc(this, GCHandleType.Pinned);
        if (mObjHandle != null)
        {
            // Define a basic DSP that receives a callback each mix to capture audio
            mDSPDescription = new FMOD.DSP_DESCRIPTION();
            mDSPDescription.numinputbuffers = 1;
            mDSPDescription.numoutputbuffers = 1;
            mDSPDescription.read = mReadCallback;
            mDSPDescription.userdata = GCHandle.ToIntPtr(mObjHandle);
        }
        else
        {
            Debug.LogWarningFormat("FMOD: Unable to create a GCHandle: mObjHandle");
        }

        // Don't start recording until DSPs have been created
        mRecording = false;
        mDSPsCreated = false;

    }

    void Update()
    {
        // Bus' underlying ChannelGroup needs to be active before DSPs can be added
        if (!mDSPsCreated)
        {
            FMOD.Studio.Bus bus = FMODUnity.RuntimeManager.GetBus(mBusPath);
            mRecording = AddDSP(bus);
            mDSPsCreated = mRecording;
            if (mRecording) Debug.Log("FMOD: Started capturing audio data");
        }

        if (mDSPsCreated && mRecording)
        {
            // When recording, press R to stop writing to file
            if (Input.GetKey(KeyCode.R))
            {
                Debug.Log("FMOD: Stopped capturing audio data, writing audio to file at " + mFilePath);
                mRecording = false;
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                mFilePath = Path.Combine(Application.dataPath, $"{timestamp}.wav"); fs = File.Create(mFilePath);
                bw = new BinaryWriter(fs);
                WriteWavHeader(mAudioData.Count);
                byte[] bytes = new byte[mAudioData.Count * 4];
                Buffer.BlockCopy(mAudioData.ToArray(), 0, bytes, 0, bytes.Length);
                fs.Write(bytes);
                fs.Close();
                bw.Close();
                mAudioData.Clear();
            }


        }
    }

    bool AddDSP(FMOD.Studio.Bus bus)
    {
        FMOD.DSP captureDSP = new FMOD.DSP();
        FMOD.ChannelGroup cg;
        if (bus.getChannelGroup(out cg) == FMOD.RESULT.OK)
        {
            if (FMODUnity.RuntimeManager.CoreSystem.createDSP(ref mDSPDescription, out captureDSP) == FMOD.RESULT.OK)
            {
                if (cg.addDSP(0, captureDSP) != FMOD.RESULT.OK)
                {
                    Debug.LogWarningFormat("FMOD: Unable to add DSP to the bus' channel group");
                }
                else
                {
                    mDSP = captureDSP;
                    mCG = cg;
                    return true;
                }
            }
            else
            {
                Debug.LogWarningFormat("FMOD: Unable to create a DSP");
            }
        }
        else
        {
            Debug.LogWarningFormat("FMOD: Unable to get bus' channel group");
        }
        captureDSP.release();
        return false;
    }

    void RemoveDSP()
    {
        // If the DSP is valid, remove it from the ChannelGroup and release
        if (mDSP.hasHandle())
        {
            mCG.removeDSP(mDSP);
            mDSP.release();
        }
    }

    void OnDestroy()
    {
        Debug.Log("FMOD: Stopped capturing audio data, writing audio to file at " + mFilePath);
        mRecording = false;
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        mFilePath = Path.Combine(Application.dataPath, $"{timestamp}.wav"); fs = File.Create(mFilePath);
        bw = new BinaryWriter(fs);
        WriteWavHeader(mAudioData.Count);
        byte[] bytes = new byte[mAudioData.Count * 4];
        Buffer.BlockCopy(mAudioData.ToArray(), 0, bytes, 0, bytes.Length);
        fs.Write(bytes);
        fs.Close();
        bw.Close();
        mAudioData.Clear();
        if (mObjHandle != null)
        {
            RemoveDSP();
            mObjHandle.Free();
        }
        
    }

    void WriteWavHeader(int length)
    {
        bw.Seek(0, SeekOrigin.Begin);

        bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));      //RIFF                          4 bytes chars
        bw.Write(32 + length * 4 - 8);                              //File Size (after this chunk)  4 bytes int     (32 for rest of header + wave data)

        bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));  //WAVEfmt                       8 bytes chars
        bw.Write(16);                                               //Length of above fmt data      4 bytes int
        bw.Write((short)3);                                         //Format 1 is PCM               2 bytes short
        bw.Write((short)mNumChannels);                              //Number of Channels            2 bytes short
        bw.Write(mSampleRate);                                      //Sample Rate                   4 bytes int
        bw.Write(mSampleRate * mBitDepth / 8 * mNumChannels);       //                              4 bytes int
        bw.Write((short)(mBitDepth / 8 * mNumChannels));            //                              2 bytes short
        bw.Write((short)mBitDepth);                                 //Bits per sample               2 bytes short

        bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));      //data                          4 bytes chars
        bw.Write(length * 4);                                       //Size of data section          4 bytes int
    }
}