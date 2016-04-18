using UnityEngine;
using System.Collections;

public class RoadRenderer : MonoBehaviour {
    Texture2D texture;
    public Renderer renderer;
    public Texture2D font;
    
    public Texture2D[] cars;

    public Texture2D[] backgrounds;

    public Texture2D title;
    public Texture2D winner;
    public Texture2D[] trees;

    public Texture2D target;
    
    Color32[] frameBuffer;
    
    public Color32[] palette;
    Color32[] fontPixels;
    
    RoadManager roadManager;
    
    void setPixel(int x, int y, Color32 c)
    {
        if (x < 0 || x>= 64 || y < 0 || y >=64)
        {
            return;
        }
        frameBuffer[64*y + x] = c;
    }
    
    void drawChar(int c, int x, int y, int paletteIndex)
    {
        int fontWidth = font.width;
        int charStart = c * 5;
        
        for (int i = 0; i < 5; ++i)
        {
            for (int j = 0; j < 7; ++j)
            {
                Color32 fp = fontPixels[charStart + j * fontWidth + i];
                //Debug.Log("fp "+i+" " + j + " " + fp);
                
                if (fp.a > 127)
                {
                    setPixel(x+ i, y + j, palette[paletteIndex]);                            
                }
            }
        }        
    }
    
    int findChar(char c)
    {
        if (c >= 'A' && c <= 'Z')
        {
            return c - 'A';
        }
        
        if (c >= '0' && c <= '9')
        {
            return c - '0' + 26;
        }
        
        if (c == '[')
        {
            return 50;
        }
        if (c == ']')
        {
            return 51;
        }
        
        return -1;
    }
    
    public void drawString(string s, int x, int y, int paletteIndex)
    {
        for (int i = 0; i < s.Length; ++i)
        {
            char c = s[i];
            int charIndex = findChar(c);
            if (charIndex < 0)
            {
                continue;
            }
            drawChar(charIndex, x + 6*i, y, paletteIndex);        
        }    
    }
    
    void MakeFont()
    {
        Color32 o = new Color32(0,0,0,0);
        Color32 x = new Color32(255, 255, 255, 255);
        
        int lineWidth = 64 * 5;
        int line = 0;
        fontPixels[0] = x;
        fontPixels[1] = o;
        fontPixels[2] = o;
        fontPixels[3] = o;
        fontPixels[4] = x;
        line += lineWidth;
        // Line 1
        fontPixels[line + 0] = x;
        fontPixels[line + 1] = o;
        fontPixels[line + 2] = o;
        fontPixels[line + 3] = o;
        fontPixels[line + 4] = x;
        line += lineWidth;
        // Line 2
        fontPixels[line + 0] = x;
        fontPixels[line + 1] = x;
        fontPixels[line + 2] = x;
        fontPixels[line + 3] = x;
        fontPixels[line + 4] = x;
        line += lineWidth;
        // Line 3
        fontPixels[line + 0] = o;
        fontPixels[line + 1] = x;
        fontPixels[line + 2] = o;
        fontPixels[line + 3] = x;
        fontPixels[line + 4] = o;
        line += lineWidth;
        // Line 4
        fontPixels[line + 0] = o;
        fontPixels[line + 1] = o;
        fontPixels[line + 2] = x;
        fontPixels[line + 3] = o;
        fontPixels[line + 4] = o;
    }
    
    public void DrawBox(int left, int top, int right, int bottom, int paletteIndex)
    {
        for (int x = left; x <= right; ++x)
        {
            for (int y = bottom; y <= top; ++y)
            {
                setPixel(x,y,palette[paletteIndex]);
            }
        }
    }
    
    public void Hlin(int left, int right, int at, int paletteIndex)
    {
        for (int x = left; x <= right; ++x)
        {
            setPixel(x, at, palette[paletteIndex]);
        }
    }

    public void Hlin32(int left, int right, int at, Color32 color)
    {
        for (int x = left; x <= right; ++x)
        {
            setPixel(x, at, color);
        }
    }
    

    public void Vlin(int bottom, int top, int at, int paletteIndex)
    {
        for (int y = bottom; y <= top; ++y)
        {
            setPixel(at, y, palette[paletteIndex]);
        }
    }

    public void VlinRGB(int bottom, int top, int at, int red, int green, int blue)
    {
        Color32 c = new Color32((byte)red, (byte)green, (byte)blue, 255);
        for (int y = bottom; y <= top; ++y)
        {
            setPixel(at, y, c);
        }
    }    
    
    public void ClearScreen(int paletteIndex)
    {
        DrawBox(0, 63, 63, 0, paletteIndex);
    }

    void Start () {
        texture = new Texture2D(64,64);
        renderer.material.mainTexture = texture;
        texture.filterMode = FilterMode.Point;
        Debug.Log("texture:" + texture);
        
        frameBuffer = new Color32[64*64];
        palette = new Color32[16];
        
        palette[0] = new Color32(0, 0, 0, 255);
        palette[1] = new Color32(227,  30,  96, 255); // brick red
        palette[2] = new Color32( 96,  78, 189, 255); // purple
        palette[3] = new Color32(255,  68, 253, 255); 
        palette[4] = new Color32(  0, 163,  96, 255);
        palette[5] = new Color32(128, 128, 128, 255); // med gray
        palette[6] = new Color32( 20, 207, 253, 255);
        palette[7] = new Color32(208, 195, 255, 255); // lavender
        palette[8] = new Color32( 96, 114,   3, 255);
        palette[9] = new Color32(255, 106,  60, 255); // orange
        palette[10] = new Color32(156, 156, 156, 255); // lighter gray
        palette[11] = new Color32(255, 160, 208, 255); // pink
        palette[12] = new Color32( 20, 245,  60, 255); // aqua
        palette[13] = new Color32(208, 221, 141, 255);
        palette[14] = new Color32(114, 255, 208, 255);
        palette[15] = new Color32(255, 255, 255, 255);
        
        fontPixels = font.GetPixels32();
        
        for (int i = 0; i < 5; ++i)
        {
            Debug.Log("fp "+i+" " + fontPixels[i]);
        }
        //MakeFont();
        
        for (int i = 0; i < font.width; ++ i)
        {
            for (int j = 0; j < font.height; ++j)
            {
                Color32 fp = fontPixels[j*font.width + i];
                if (fp.a < 128)
                {
                    fontPixels[j*font.width + i] = new Color(0, 0, 0, 0);
                }
            }
        }
        
        roadManager = RoadManager.Instance;
        roadManager.Start();
    }

    void drawOSLogo()
    {    
        drawString("BDGOS ][", 8, 55, 15);
    }
    
    void drawColorBars()
    {    
        for (int i=0; i<16; ++i)
        {
            DrawBox(i*4, 63, i*4+3, 0, i);
        }
    }
    
    public void drawSprite(Texture2D tex, int x, int y, bool centerX, bool centerY)
    {
        int width = tex.width;
        int height = tex.height;
        int offsetX = centerX ? tex.width / 2 : 0;
        int offsetY = centerY ? tex.height / 2 : 0;
        Color32[] texPixels = tex.GetPixels32();
        for (int i= 0; i < width; ++i)
        {
            int destX = x - offsetX + i;
            if (destX < 0 || destX > 63)
            {
                continue;
            }
            for (int j=0; j < height; ++j)
            {
                int destY = y - offsetY + j;
                if (destY < 0 || destY > 63)
                {
                    continue;
                }
                
                Color32 px = texPixels[width * j + i];
                if (px.a > 127)
                {
                    setPixel(destX, destY, px);
                }
            }
        }            
    }
    
    public void drawSpriteScaled(Texture2D tex, int x, int y, float scale)
    {
        int width = tex.width;
        int height = tex.height;
        Color32[] carPixels = tex.GetPixels32();
        for (int i= 0; i < width * scale; ++i)
        {
            int ti = (int)(i / scale);
            for (int j=0; j < height * scale; ++j)
            {
                int tj = (int)(j / scale);
                
                Color32 px = carPixels[width*tj + ti];
                if (px.a > 127)
                {
                    setPixel(x+i, y+j, px);
                }
            }
        }            
    }

    public void drawSpriteScaledAndClipped(Texture2D tex, int x, int y, float scale, int clipHeight, bool centerX, bool centerY)
    {
        int width = tex.width;
        int height = tex.height;
        int offsetX = centerX ? Mathf.FloorToInt(width * scale / 2) : 0;
        int offsetY = centerY ? Mathf.FloorToInt(height * scale / 2) : 0;
        Color32[] carPixels = tex.GetPixels32();
        for (int j=0; j < height * scale; ++j)
        {
            int destY = y + j - offsetY;
            if (destY < clipHeight || destY < 0 || destY >= 64)
            {
                continue;
            }
            int tj = (int)(j / scale);
            
            for (int i= 0; i < width * scale; ++i)
            {
                int destX = x + i - offsetX;
                if (destX < 0 || destX >= 64)
                {
                    continue;
                }
                int ti = (int)(i / scale);
                
                Color32 px = carPixels[width*tj + ti];
                if (px.a > 127)
                {
                    setPixel(destX, destY, px);
                }
            }
        }            
    }
    
    
    /*
    void drawScalingBoxTest()
    {  
        // used to be globals
        int at = 0;    
        Color32 c;
        int cnum = 0;


        at++;
        if (at > texture.width)
        {
            at = 0;
            cnum += 1;
            Debug.Log("cnum" + cnum);
            //byte r = (byte) (255 * ((cnum & 0x4) >> 2));
            //byte g = (byte) (255 *((cnum & 0x2) >> 1));
            //byte b = (byte) (255 * (cnum & 0x1));
            
            //c = new Color32(r, g, b, 255);
            //c = new Color32(255, 128, 64, 255);
            c = palette[cnum % 16];
        }
         
        for (int i = 0; i < at; ++i)
        {
            for (int j = 0; j < at; ++j)
            {
                frameBuffer[j*64+i] = c;
            }
        }
    }*/
    
    int frameCount = 0;
    void Update () {
        frameCount++;

        if (frameCount < 35)
        {
            drawOSLogo();
        }
        else
        {
            //float t = (frameCount-50) / (1024.0f-50);

                /*
            float c1pos = 100-100*t;
            float c2pos = 75-50*t;
            
            float s1 = 1.0f-(c1pos / 100);
            float s2 = 1.0f-(c2pos / 100);
            
            int y1 = (int)c1pos/2;
            int y2 = (int)c2pos/2;
            
            float xf1 = .5f;
            float xf2 = -.25f;
            
            int x1 = 32+(int)(22 * xf1 * s1);
            int x2 = 32+(int)(22 * xf2 * s2);*/
            
            roadManager.Tick(this);

                /*
            if (y1 < y2)
            {
                drawSpriteScaled(cars[2], x2, y2, s2);
                drawSpriteScaled(cars[1], x1, y1, s1);
            }
            else
            {
                drawSpriteScaled(cars[1], x1, y1, s1);
                drawSpriteScaled(cars[2], x2, y2, s2);
            }*/
        }
        
        texture.SetPixels32(frameBuffer);
        texture.Apply();
    }
}
