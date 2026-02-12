# Development Instructions

## Local Development

```bash
# Install dependencies
npm install

# Start dev server
npm run dev

# Preview production build locally
npm run preview

# Production build
npm run build

# Format code
npm run format
```

## Project Structure

- `config/` — Hugo configuration (per-environment)
- `content/` — Markdown content files
- `layouts/` — Hugo templates
- `assets/css/` — Tailwind CSS (v4)
- `assets/scss/` — Legacy SCSS (migration in progress)
- `static/` — Static files served as-is
- `tools/` — .NET ImageProcessor for image optimization
- `infra/` — Bicep IaC for Azure Static Web Apps

## Image Processing

Images are managed via the .NET ImageProcessor tool in `tools/`:

```bash
# Push/process images
bash tools/scripts/push-images.sh
```

## Deployment

- **CI/CD**: GitHub Actions (`.github/workflows/main.yml`)
- **Infrastructure**: Bicep via `.github/workflows/infra-swa.yml`
- **Hosting**: Azure Static Web Apps
- **Auth**: OIDC (Azure Login v2)
