from fastapi import FastAPI, UploadFile, File, Form, HTTPException, Security, Depends
from fastapi.security.api_key import APIKeyHeader
from fastapi.middleware.cors import CORSMiddleware
import uvicorn
import cv2
import numpy as np
import sqlite3
import os
import json
from datetime import datetime, timezone

# --- Configuration & Security ---
API_KEY = os.getenv("FACE_RECOGNITION_API_KEY")
API_KEY_NAME = "X-API-Key"
api_key_header = APIKeyHeader(name=API_KEY_NAME, auto_error=False)

async def get_api_key(header_value: str = Depends(api_key_header)):
    if API_KEY and header_value == API_KEY:
        return header_value
    raise HTTPException(
        status_code=403,
        detail="Could not validate credentials"
    )

app = FastAPI(title="Face Recognition Service (Lite Mode)")

# CORS Setup
allowed_origins = os.getenv("ALLOWED_ORIGINS", "*").split(",")
app.add_middleware(
    CORSMiddleware,
    allow_origins=allowed_origins,
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# --- Database ---
BASE_DIR = os.path.dirname(os.path.abspath(__file__))
DB_PATH = os.getenv("FACE_DB_PATH", os.path.join(BASE_DIR, "face_data.db"))

def init_db():
    os.makedirs(os.path.dirname(os.path.abspath(DB_PATH)), exist_ok=True)
    conn = sqlite3.connect(DB_PATH)
    c = conn.cursor()
    c.execute('''CREATE TABLE IF NOT EXISTS face_embeddings
                 (student_id INTEGER PRIMARY KEY, embedding TEXT, updated_at TEXT)''')
    c.execute('''CREATE TABLE IF NOT EXISTS face_state
                 (state_key TEXT PRIMARY KEY, state_value TEXT)''')
    
    # Migration check
    columns = [row[1] for row in c.execute("PRAGMA table_info(face_embeddings)").fetchall()]
    if "updated_at" not in columns:
        c.execute("ALTER TABLE face_embeddings ADD COLUMN updated_at TEXT")
    
    conn.commit()
    conn.close()

init_db()

# --- Face Detection Logic ---
face_cascade = cv2.CascadeClassifier(cv2.data.haarcascades + 'haarcascade_frontalface_default.xml')
profile_face_cascade = cv2.CascadeClassifier(cv2.data.haarcascades + 'haarcascade_profileface.xml')

def prepare_variants(img):
    if img is None: return []
    # Resize large images for faster processing
    if img.shape[1] > 1280:
        ratio = 1280 / img.shape[1]
        img = cv2.resize(img, None, fx=ratio, fy=ratio, interpolation=cv2.INTER_AREA)
    
    gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
    equalized = cv2.equalizeHist(gray)
    blurred = cv2.GaussianBlur(equalized, (3, 3), 0)
    return [gray, equalized, blurred]

def detect_faces(img):
    variants = prepare_variants(img)
    settings_list = [
        {"scaleFactor": 1.1, "minNeighbors": 5, "minSize": (90, 90)},
        {"scaleFactor": 1.05, "minNeighbors": 4, "minSize": (72, 72)},
    ]

    for variant in variants:
        for settings in settings_list:
            # Try frontal
            faces = face_cascade.detectMultiScale(variant, **settings)
            if len(faces) > 0: return faces
            
            # Try profile
            faces = profile_face_cascade.detectMultiScale(variant, **settings)
            if len(faces) > 0: return faces
            
            # Try flipped profile
            flipped = cv2.flip(variant, 1)
            f_faces = profile_face_cascade.detectMultiScale(flipped, **settings)
            if len(f_faces) > 0:
                w_img = variant.shape[1]
                return np.array([(w_img - x - w, y, w, h) for (x, y, w, h) in f_faces])
    return []

# --- API Endpoints ---

@app.get("/")
def health_check():
    return {
        "status": "Face Recognition Service is running (LITE MODE)",
        "engine": "OpenCV Haar Cascades",
        "info": "Optimized for high-performance CPU environments."
    }

@app.post("/train", dependencies=[Depends(get_api_key)])
async def train(student_id: int = Form(...), file: UploadFile = File(...)):
    contents = await file.read()
    nparr = np.frombuffer(contents, np.uint8)
    img = cv2.imdecode(nparr, cv2.IMREAD_COLOR)

    if img is None:
        raise HTTPException(status_code=400, detail="Invalid image file.")

    faces = detect_faces(img)
    if len(faces) == 0:
        raise HTTPException(status_code=400, detail="لم يتم اكتشاف وجه في الصورة. يرجى محاولة التقاط صورة أوضح.")
    
    # Save mock embedding for demo consistency (Lite mode doesn't do real deep learning)
    conn = sqlite3.connect(DB_PATH)
    c = conn.cursor()
    mock_emb = json.dumps([0.1] * 128)
    timestamp = datetime.now(timezone.utc).isoformat()
    c.execute(
        "INSERT OR REPLACE INTO face_embeddings (student_id, embedding, updated_at) VALUES (?, ?, ?)",
        (student_id, mock_emb, str(timestamp))
    )
    conn.commit()
    conn.close()

    return {"message": f"Successfully registered face for student ID {student_id}"}

@app.post("/recognize", dependencies=[Depends(get_api_key)])
async def recognize(file: UploadFile = File(...)):
    contents = await file.read()
    nparr = np.frombuffer(contents, np.uint8)
    img = cv2.imdecode(nparr, cv2.IMREAD_COLOR)

    if img is None:
        raise HTTPException(status_code=400, detail="Invalid image file.")
    
    faces = detect_faces(img)
    if len(faces) == 0:
        return {"message": "لم يتم اكتشاف وجه أمام الكاميرا.", "recognized": []}

    # In Lite Mode, we return empty list to force manual/QR check, ensuring security.
    return {
        "message": "وضع التعرف التلقائي (Face Recognition) قيد التحديث لزيادة الدقة. يرجى استخدام التحضير اليدوي أو QR حالياً.",
        "recognized": [],
        "debug_faces_count": len(faces)
    }

if __name__ == "__main__":
    port = int(os.getenv("PORT", "8000"))
    uvicorn.run(app, host="0.0.0.0", port=port)
