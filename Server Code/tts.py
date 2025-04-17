ELEVENLABS_API_KEY = ''

import requests
import io
import base64
import time
import os
from pydub import AudioSegment

# Constants
ELEVENLABS_VOICE_ID = "z6Kj0hecH20CdetSElRT"           # Replace with your desired voice ID from ElevenLabs
ELEVENLABS_API_URL = f"https://api.elevenlabs.io/v1/text-to-speech/{ELEVENLABS_VOICE_ID}"
AUDIO_SAVE_PATH = "saved_audios"  # Directory to save TTS audio files

#Ensure the audio save directory exists
os.makedirs(AUDIO_SAVE_PATH, exist_ok=True)

def generate_tts_audio(text, voice_id=None):
    #Send text to ElevenLabs TTS API, save the audio file, and return its Base64-encoded string.

    #Args:
    #    text (str): The text to be converted to speech.
    #    voice_id (str, optional): The ID of the voice to use. Defaults to ELEVENLABS_VOICE_ID.
    # Returns:
    #    str: Base64-encoded audio string or None if an error occurs.
    
    print('Generating TTS using ElevenLabs API...')
     # Use default voice_id if not provided
    if voice_id is None:
        voice_id = ELEVENLABS_VOICE_ID
    headers = {
        "Accept": "audio/mpeg",  # Change to "audio/wav" if you prefer WAV format
        "Content-Type": "application/json",
        "xi-api-key": ELEVENLABS_API_KEY
    }

    payload = {
        "text": text,
        "model_id": "eleven_multilingual_v2",
        "voice_settings": {
            "stability": 0.75,
            "similarity_boost": 0.75
        }
    }


    try:
        response = requests.post(ELEVENLABS_API_URL, headers=headers, json=payload)

        # Debugging: Print response status and headers
        print(f"Response Status Code: {response.status_code}")
        print(f"Response Headers: {response.headers}")

        if response.status_code == 200:
            content_type = response.headers.get('Content-Type', '')
            print(f"Content-Type: {content_type}")

            if 'audio' not in content_type:
                print("Error: The response does not contain audio data.")
                print(f"Response Content: {response.text}")
                return None

            # Save raw audio for inspection
            audio_format = content_type.split('/')[-1]
            timestamp = int(time.time())
            raw_audio_filename = f"raw_tts_{timestamp}.{audio_format}"
            raw_audio_path = os.path.join(AUDIO_SAVE_PATH, raw_audio_filename)
            with open(raw_audio_path, "wb") as f:
                f.write(response.content)
            print(f"Raw audio saved to {raw_audio_path}")

            # Proceed with pydub processing
            try:
                # Specify the correct format here
                audio = AudioSegment.from_file(io.BytesIO(response.content), format="mp3")
            except Exception as e:
                print(f"pydub failed to parse audio: {e}")
                return None

            # Process audio: convert to mono, set sample width and frame rate
            audio = audio.set_channels(1)  # Mono
            audio = audio.set_sample_width(2)  # 16 bits
            audio = audio.set_frame_rate(16000)  # 16kHz

            # Export to WAV format in memory
            linear16_io = io.BytesIO()
            audio.export(linear16_io, format="wav")
            linear16_io.seek(0)

            # Optionally, save the processed audio file
            processed_audio_filename = f"tts_{timestamp}.wav"
            processed_audio_path = os.path.join(AUDIO_SAVE_PATH, processed_audio_filename)
            with open(processed_audio_path, "wb") as f:
                f.write(linear16_io.read())
            print(f"Processed audio saved to {processed_audio_path}")

            # Reset the buffer to read for Base64 encoding
            linear16_io.seek(0)
            pcm_data = linear16_io.read()
            pcm_base64 = base64.b64encode(pcm_data).decode('utf-8')

            return pcm_base64

        else:
            print(f"Error: {response.status_code}, {response.text}")
            return None

    except Exception as e:
        print(f"An error occurred while requesting TTS: {e}")
        return None

# Example usage
if __name__ == "__main__":
    start_time = time.time()
    audio_base64 = generate_tts_audio("안녕하세요")
    end_time = time.time()

    if audio_base64:
        print("Base64 Encoded Audio:")
        print(audio_base64)
    print(f"Time taken: {end_time - start_time:.2f} seconds")