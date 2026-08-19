﻿﻿// imports
using System.Collections;
using System.Collections.Generic;
using UnityEngine;



// class
public class PipBoyRadioController : MonoBehaviour
{

    // variables
    // The audio source that plays the radio tracks.
    [SerializeField] private AudioSource radioSource;

    // Whether the radio is powered on.
    [SerializeField] private bool radioOn;

    // Which station is currently selected.
    [SerializeField] private RadioStation currentStation = RadioStation.Station1;

    // Tracks for station 1.
    [SerializeField] private AudioClip[] station1Tracks;

    // Tracks for station 2.
    [SerializeField] private AudioClip[] station2Tracks;

    // If true, reshuffle the station order each time the station changes.
    [SerializeField] private bool reshuffleOnStationChange = true;

    // Internal playback routine handle.
    private Coroutine playRoutine;

    // Internal queue for the current station.
    private List<AudioClip> currentQueue = new List<AudioClip>();

    // Internal index into the queue.
    private int queueIndex;



    // methods
    private void Reset()
    {
        // Auto-assign the AudioSource when the component is added.
        radioSource = GetComponent<AudioSource>();
    }


    private void Awake()
    {
        // If no AudioSource was assigned, try to grab one from this GameObject.
        if (!radioSource)
            radioSource = GetComponent<AudioSource>();

        // Ensure the source doesn't loop a single clip, because we manage sequencing.
        if (radioSource)
            radioSource.loop = false;
    }


    private void Start()
    {
        // Apply the inspector state at runtime startup.
        ApplyInspectorState();
    }


    private void OnValidate()
    {
        // Avoid doing runtime work when not playing.
        if (!Application.isPlaying)
            return;

        // Apply changes whenever you tweak values in the inspector during play mode.
        ApplyInspectorState();
    }


    private void ApplyInspectorState()
    {
        // Stop if we don't have an AudioSource.
        if (!radioSource)
            return;

        // If radio should be on, start or continue playback.
        if (radioOn)
            EnsurePlaying();
        else
            // If radio should be off, stop playback.
            StopRadio();
    }


    private void EnsurePlaying()
    {
        // Stop if already running a routine.
        if (playRoutine != null)
            return;

        // Build a queue from the current station.
        BuildQueueForStation(currentStation);

        // Start the playback routine.
        playRoutine = StartCoroutine(PlayQueueRoutine());
    }


    private void StopRadio()
    {
        // Stop the coroutine if it exists.
        if (playRoutine != null)
            StopCoroutine(playRoutine);

        // Clear the routine handle.
        playRoutine = null;

        // Stop the audio source immediately.
        if (radioSource)
            radioSource.Stop();
    }


    public void SetStation(RadioStation station)
    {
        // Store the new station.
        currentStation = station;

        // If radio is off, do nothing else.
        if (!radioOn)
            return;

        // If requested, reshuffle when switching station.
        if (reshuffleOnStationChange)
        {
            // Stop current playback.
            StopRadio();

            // Restart playback on the new station.
            EnsurePlaying();
        }
        else
        {
            // Just rebuild the queue but keep playing flow.
            BuildQueueForStation(currentStation);
        }
    }


    public void ToggleStation1()
    {
        // If station 1 is already playing, turn the radio off.
        if (radioOn && currentStation == RadioStation.Station1)
        {
            radioOn = false;
            StopRadio();
            return;
        }

        // Otherwise, switch to station 1 and ensure playback.
        radioOn = true;
        SetStation(RadioStation.Station1);
        EnsurePlaying();
    }


    public void ToggleStation2()
    {
        // If station 2 is already playing, turn the radio off.
        if (radioOn && currentStation == RadioStation.Station2)
        {
            radioOn = false;
            StopRadio();
            return;
        }

        // Otherwise, switch to station 2 and ensure playback.
        radioOn = true;
        SetStation(RadioStation.Station2);
        EnsurePlaying();
    }


    private void BuildQueueForStation(RadioStation station)
    {
        // Clear the old queue.
        currentQueue.Clear();

        // Reset the queue index.
        queueIndex = 0;

        // Choose tracks based on station.
        AudioClip[] tracks = station == RadioStation.Station1 ? station1Tracks : station2Tracks;

        // Stop if there are no tracks assigned.
        if (tracks == null || tracks.Length == 0)
            return;

        // Add all tracks into the queue.
        for (int i = 0; i < tracks.Length; i++)
        {
            // Skip null entries.
            if (tracks[i] == null)
                continue;

            // Add the clip to the queue.
            currentQueue.Add(tracks[i]);
        }

        // Shuffle the queue for random order playback.
        if (currentQueue.Count > 1)
            Shuffle(currentQueue);
    }


    private IEnumerator PlayQueueRoutine()
    {
        // Keep playing while the radio is on.
        while (radioOn)
        {
            // If the queue is empty, wait until it gets populated.
            if (currentQueue.Count == 0)
            {
                // Wait one frame and try again.
                yield return null;

                // Continue the loop.
                continue;
            }

            // If we've reached the end of the queue, reshuffle and restart.
            if (queueIndex >= currentQueue.Count)
            {
                // Reshuffle to get a new random order.
                if (currentQueue.Count > 1)
                    Shuffle(currentQueue);

                // Reset the index.
                queueIndex = 0;
            }

            // Get the next clip to play.
            AudioClip clipToPlay = currentQueue[queueIndex];

            // Advance the queue index for next time.
            queueIndex++;

            // Assign the clip to the AudioSource.
            radioSource.clip = clipToPlay;

            // Play the clip.
            radioSource.Play();

            // Wait until the clip finishes or until the radio is turned off.
            while (radioOn && radioSource.isPlaying)
                yield return null;
        }

        // If we exit the loop, ensure we clean up.
        StopRadio();
    }


    private void Shuffle(List<AudioClip> list)
    {
        if (list == null || list.Count < 2)
            return;

        // Fisher-Yates shuffle.
        for (int i = list.Count - 1; i > 0; i--)
        {
            // Pick a random index from 0..i.
            int j = Random.Range(0, i + 1);

            // Swap elements i and j.
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    
    // station enum
    public enum RadioStation
    {
        Station1,

        Station2
    }
}
