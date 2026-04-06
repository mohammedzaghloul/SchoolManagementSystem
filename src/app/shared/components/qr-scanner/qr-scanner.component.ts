// shared/components/qr-scanner/qr-scanner.component.ts
import { Component, ElementRef, EventEmitter, Input, OnDestroy, OnInit, Output, ViewChild } from '@angular/core';
import { Html5Qrcode } from 'html5-qrcode';

@Component({
  selector: 'app-qr-scanner',
  templateUrl: './qr-scanner.component.html',
  styleUrls: ['./qr-scanner.component.css']
})
export class QrScannerComponent implements OnInit, OnDestroy {
  @ViewChild('qrReader') qrReader!: ElementRef;
  @Output() scanSuccess = new EventEmitter<string>();
  @Output() scanError = new EventEmitter<string>();
  @Input() fps = 10;
  @Input() qrbox = 250;
  
  private html5QrCode: Html5Qrcode | null = null;
  isScanning = false;
  hasCamera = false;
  cameras: MediaDeviceInfo[] = [];
  selectedCamera: string = '';

  async ngOnInit() {
    await this.checkCameras();
  }

  async checkCameras() {
    try {
      const devices = await navigator.mediaDevices.enumerateDevices();
      this.cameras = devices.filter(device => device.kind === 'videoinput');
      this.hasCamera = this.cameras.length > 0;
      
      if (this.hasCamera) {
        this.selectedCamera = this.cameras[0].deviceId;
      }
    } catch (error) {
      console.error('Error checking cameras:', error);
      this.hasCamera = false;
    }
  }

  async startScanner(cameraId?: string) {
    if (!this.hasCamera) return;
    
    this.isScanning = true;
    
    const cameraToUse = cameraId || this.selectedCamera;
    
    this.html5QrCode = new Html5Qrcode('qr-reader');
    
    const qrCodeSuccessCallback = (decodedText: string) => {
      this.scanSuccess.emit(decodedText);
      this.stopScanner();
    };

    const config = {
      fps: this.fps,
      qrbox: this.qrbox,
      aspectRatio: 1.0
    };

    try {
      await this.html5QrCode.start(
        cameraToUse,
        config,
        qrCodeSuccessCallback,
        (errorMessage) => {
          this.scanError.emit(errorMessage);
        }
      );
    } catch (error) {
      console.error('Error starting scanner:', error);
      this.isScanning = false;
    }
  }

  async switchCamera() {
    if (this.cameras.length < 2) return;
    
    await this.stopScanner();
    
    const currentIndex = this.cameras.findIndex(c => c.deviceId === this.selectedCamera);
    const nextIndex = (currentIndex + 1) % this.cameras.length;
    this.selectedCamera = this.cameras[nextIndex].deviceId;
    
    await this.startScanner(this.selectedCamera);
  }

  async stopScanner() {
    if (this.html5QrCode && this.html5QrCode.isScanning) {
      await this.html5QrCode.stop();
      this.html5QrCode.clear();
      this.isScanning = false;
    }
  }

  ngOnDestroy() {
    this.stopScanner();
  }
}