import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { VideoService } from '../../../core/services/video.service';
import { Video } from '../../../core/models/video.model';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';

@Component({
  selector: 'app-videos',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './videos.component.html',
  styleUrls: ['./videos.component.css']
})
export class VideosComponent implements OnInit {
  videos: Video[] = [];
  filteredVideos: any[] = [];
  loading = false;
  
  showPlayer = false;
  selectedVideoUrl: SafeResourceUrl | null = null;
  activeVideo: Video | null = null;

  categories = ['الكل', 'الرياضيات', 'الفيزياء', 'اللغة العربية', 'اللغة الإنجليزية', 'العلوم'];
  activeCategory = 'الكل';

  constructor(private videoService: VideoService, private sanitizer: DomSanitizer) { }

  ngOnInit(): void {
    this.loadVideos();
  }

  async loadVideos() {
    this.loading = true;
    try {
      this.videos = await this.videoService.getVideos();
      this.filterVideos();
    } catch (err) {
      console.error(err);
      this.videos = [];
      this.filteredVideos = [];
    } finally {
      this.loading = false;
    }
  }

  setCategory(cat: string) {
    this.activeCategory = cat;
    this.filterVideos();
  }

  filterVideos() {
    if (this.activeCategory === 'الكل') {
      this.filteredVideos = [...this.videos];
    } else {
      this.filteredVideos = this.videos.filter(v => v.subject === this.activeCategory);
    }
  }

  playVideo(video: Video) {
    const youtubeId = this.extractYouTubeId(video.url);
    if (youtubeId) {
      this.selectedVideoUrl = this.sanitizer.bypassSecurityTrustResourceUrl(`https://www.youtube.com/embed/${youtubeId}?autoplay=1`);
      this.activeVideo = video;
      this.showPlayer = true;
    } else {
      // For non-YouTube or invalid IDs, open in a new tab to avoid frame errors
      window.open(video.url, '_blank');
    }
  }

  private extractYouTubeId(url: string): string | null {
    if (!url) return null;
    const regExp = /^.*((youtu.be\/)|(v\/)|(\/u\/\w\/)|(embed\/)|(watch\?v=)|(\?v=)|(shorts\/)|(live\/))([^#\&\?]*).*/;
    const match = url.match(regExp);
    return (match && (match[10].length === 11)) ? match[10] : null;
  }
}
