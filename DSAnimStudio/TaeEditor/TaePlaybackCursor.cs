﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using SoulsFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DSAnimStudio.TaeEditor
{

    public class TaePlaybackCursor
    {
        public double CurrentTime;

        public string GetFrameCounterText(bool roundToNearestFrame)
        {
            return (roundToNearestFrame ?
                    $"{(int)(GUICurrentFrame % MaxFrame)}" :
                    $"{(MaxFrame <= 0 ? 0 : (GUICurrentFrame % MaxFrame)):F02}") +
                    $"/{(int)((Math.Max(Math.Round(MaxFrame), 0)))}";
        }

        public int CurrentLoopCount { get; private set; } = 0;
        public int OldLoopCount { get; private set; } = 0;
        public int CurrentLoopCountDelta { get; private set; } = 0;

        public double CurrentTimeMod => CurrentTime % MaxTime;

        public event EventHandler<TaeEditAnimEventBox> EventBoxEnter;
        private void OnEventBoxEnter(TaeEditAnimEventBox evBox) { EventBoxEnter?.Invoke(this, evBox); }

        public event EventHandler<TaeEditAnimEventBox> EventBoxMidst;
        private void OnEventBoxMidst(TaeEditAnimEventBox evBox) { EventBoxMidst?.Invoke(this, evBox); }

        public event EventHandler<TaeEditAnimEventBox> EventBoxExit;
        private void OnEventBoxExit(TaeEditAnimEventBox evBox) { EventBoxExit?.Invoke(this, evBox); }

        public event EventHandler PlaybackStarted;
        private void OnPlaybackStarted() { PlaybackStarted?.Invoke(this, EventArgs.Empty); }

        public event EventHandler PlaybackLooped;
        private void OnPlaybackLooped() { PlaybackLooped?.Invoke(this, EventArgs.Empty); }

        public event EventHandler PlaybackEnded;
        private void OnPlaybackEnded() { PlaybackEnded?.Invoke(this, EventArgs.Empty); }

        public event EventHandler PlaybackFrameChange;
        private void OnPlaybackFrameChange() { PlaybackFrameChange?.Invoke(this, EventArgs.Empty); }

        public event EventHandler ScrubFrameChange;
        private void OnScrubFrameChange() { ScrubFrameChange?.Invoke(this, EventArgs.Empty); }

        public double GUICurrentTime => Main.TAE_EDITOR.Config.LockFramerateToOriginalAnimFramerate 
            ? (Math.Round(CurrentTime / (SnapInterval ?? SnapInterval_Default)) 
            * (SnapInterval ?? SnapInterval_Default)) : CurrentTime;

        //public double HitWindowStart => CurrentTime - 0.001;
        //public double HitWindowEnd => CurrentTime + 0.001;

        //public double OldHitWindowStart => OldCurrentTime;
        //public double OldHitWindowEnd => OldCurrentTime + (CurrentSnapInterval / 2.0);

        public double GUICurrentTimeMod => GUICurrentTime % MaxTime;

        public double GUIStartTime => Main.TAE_EDITOR.Config.LockFramerateToOriginalAnimFramerate 
            ? (Math.Round(StartTime / (SnapInterval ?? SnapInterval_Default)) 
            * (SnapInterval ?? SnapInterval_Default)) : StartTime;

        public double OldGUICurrentTime { get; private set; } = 0;

        public double OldCurrentTime { get; private set; } = 0;
        public double OldCurrentTimeMod => OldCurrentTime % MaxTime;

        public double OldGUICurrentTimeMod => OldGUICurrentTime % MaxTime;

        public double OldGUICurrentFrame { get; private set; } = 0;

        public double OldGUICurrentFrameMod => OldGUICurrentFrame % MaxFrame;

        public double StartTime = 0;
        public double MaxTime { get; private set; } = 1;

        public double? HkxAnimationLength = null;
        public double? HkxAnimationFrameLength = null;

        public double? SnapInterval = null;

        public const double SnapInterval_Default = 0.0333333;

        public double CurrentSnapInterval => 0.0166666666666666;// SnapInterval ?? SnapInterval_Default;

        public double GUICurrentFrame => Main.TAE_EDITOR.Config.LockFramerateToOriginalAnimFramerate 
            ? (Math.Round(CurrentTime / CurrentSnapInterval)) 
            :  (CurrentTime / CurrentSnapInterval);

        public double GUICurrentFrameMod => MaxFrame > 0 ? (GUICurrentFrame % MaxFrame) : 0;

        public double MaxFrame => Main.TAE_EDITOR.Config.LockFramerateToOriginalAnimFramerate 
            ? (Math.Round(MaxTime / CurrentSnapInterval)) 
            : (MaxTime / CurrentSnapInterval);

        public bool IsRepeat = true;
        public bool IsPlaying = false;
        private bool prevIsPlaying = false;

        public bool Scrubbing = false;
        public bool prevScrubbing = false;

        public float BasePlaybackSpeed = 1.0f;
        public float ModPlaybackSpeed = 1.0f;

        public bool IsStepping = false;

        public bool JustStartedPlaying = false;

        public void ResetAll()
        {
            CurrentLoopCount = 0;
            CurrentLoopCountDelta = 0;
            CurrentTime = 0;
            //IsPlaying = false;
            //IsRepeat = true;
            //IsStepping = false;
            JustStartedPlaying = false;
            MaxTime = 0;
            OldGUICurrentFrame = 0;
            OldGUICurrentTime = 0;
            OldLoopCount = 0;
            //prevScrubbing = false;
            //Scrubbing = false;
            StartTime = 0;

            //PlaybackSpeed = 1.0f;
        }

        public void Transport_PlayPause()
        {
            IsStepping = false;

            IsPlaying = !IsPlaying;

            //StartTime = CurrentTime;

            if (!IsPlaying)
            {
                OnPlaybackEnded();
            }
        }

        public void Update(IEnumerable<TaeEditAnimEventBox> eventBoxes)
        {
            //bool prevPlayState = IsPlaying;

            //if (GUICurrentTime != oldGUICurrentTime)
            //{
            //    Scrubbing = true;
            //}

            //if (CurrentTime < 0)
            //    CurrentTime = 0;

            if (Scrubbing)
            {
                IsStepping = false;
            }

            JustStartedPlaying = !prevIsPlaying && IsPlaying;

            if (Scrubbing || JustStartedPlaying)
            {
                CurrentLoopCount = (int)(CurrentTime / (MaxTime));
            }

            if (JustStartedPlaying || (CurrentLoopCount != OldLoopCount))
                OnPlaybackStarted();

            if (HkxAnimationLength.HasValue)
                MaxTime = HkxAnimationLength.Value;

            if (IsPlaying || Scrubbing || IsStepping)
            {
               

                if (IsPlaying)
                {
                    CurrentTime += (Main.DELTA_UPDATE * BasePlaybackSpeed * ModPlaybackSpeed);
                }

                if (GUICurrentTime != OldGUICurrentTime)
                {
                    if (CurrentTime < 0)
                    {
                        CurrentTime += MaxTime;
                        CurrentLoopCount--;
                    }
                }

                bool justReachedAnimEnd = (CurrentTime >= MaxTime);

                // Single frame anim
                //if (MaxTime <= (SnapInterval))
                //{
                //    CurrentTime = 0;
                //}

                if (!HkxAnimationLength.HasValue)
                    MaxTime = 0;

                foreach (var box in eventBoxes)
                {
                    if (!HkxAnimationLength.HasValue)
                    {
                        if (box.MyEvent.EndTime > MaxTime)
                            MaxTime = box.MyEvent.EndTime;
                    }

                    //bool currentlyInEvent = false;
                    //bool prevFrameInEvent = false;

                    //if (Main.TAE_EDITOR.Config.EnableSnapTo30FPSIncrements)
                    //{
                    //    int currentFrame = (int)Math.Floor((GUICurrentTime % MaxTime) / SnapIntervalHz);
                    //    int prevFrame = (int)Math.Floor((oldGUICurrentTime % MaxTime) / SnapIntervalHz);
                    //    int eventStartFrame = (int)Math.Round(box.MyEvent.StartTime / SnapIntervalHz);
                    //    int eventEndFrame = (int)Math.Round(box.MyEvent.EndTime / SnapIntervalHz);

                    //    currentlyInEvent = currentFrame >= eventStartFrame && currentFrame < eventEndFrame;
                    //    prevFrameInEvent = !justStartedPlaying && prevFrame >= eventStartFrame && prevFrame < eventEndFrame;
                    //}
                    //else
                    //{
                    //    currentlyInEvent = GUICurrentTime >= box.MyEvent.StartTime && GUICurrentTime < box.MyEvent.EndTime;
                    //    prevFrameInEvent = !justStartedPlaying && oldGUICurrentTime >= box.MyEvent.StartTime && oldGUICurrentTime < box.MyEvent.EndTime;
                    //}

                    if (box.PlaybackHighlight)
                    {
                        if (!box.PrevCyclePlaybackHighlight)
                        {
                            OnEventBoxEnter(box);
                        }

                        ////Also check if we looped playback
                        //if (!prevFrameInEvent || isFirstFrameAfterLooping)
                        //{

                        //}

                        OnEventBoxMidst(box);
                    }
                    else
                    {
                        if (box.PrevCyclePlaybackHighlight)
                        {
                            OnEventBoxExit(box);
                        }
                    }

                    

                    //if (IsPlaying && justReachedAnimEnd)
                    //{
                    //    box.PlaybackHighlight = false;
                    //}

                }

                if (IsPlaying)
                {


                    if (JustStartedPlaying)
                    {
                        //CurrentTime = 0;
                    }
                    else if (MaxTime <= 0)
                    {
                        CurrentTime = MaxTime;
                        CurrentLoopCount = 0;
                    }
                    else
                    {
                        if (IsRepeat)
                        {
                            while (CurrentTime >= MaxTime)
                            {
                                CurrentTime -= MaxTime;
                                CurrentLoopCount++;
                                //previousFrameTime -= MaxTime;
                                OnPlaybackLooped();
                            }

                            // way simpler
                            //CurrentTime %= MaxTime;



                        }
                    }


                }

                if (GUICurrentTime != OldGUICurrentTime)
                {
                    if (Scrubbing)
                        OnScrubFrameChange();
                    else
                        OnPlaybackFrameChange();
                }

               

            }

            OldGUICurrentTime = GUICurrentTime;
            OldCurrentTime = CurrentTime;
            OldGUICurrentFrame = GUICurrentFrame;
            prevScrubbing = Scrubbing;

            if (CurrentTime < 0 && CurrentLoopCount == 0)
                CurrentLoopCount--;

            CurrentLoopCountDelta = CurrentLoopCount - OldLoopCount;

            OldLoopCount = CurrentLoopCount;

            prevIsPlaying = IsPlaying;
        }
    }
}
