/*
 * 本程序需在1366*768，扫雷游戏难度为中等 且 游戏处于半屏运行的状况下
 * 雷区的矩形区域为 36*36，第一个雷的左上角坐标为（65，105）
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;



namespace autoMinesweeper
{
    class Program
    {
        #region 预设的一些值

        #region 1366*768分辨率下的参数设置
        static int M_up = 120;          //半屏下 扫雷的矩形区域为 35*35，第一个雷的左上角坐标为（75，115）
        static int M_left = 75;         //偏移量，可以在半屏下截出雷区
        static int step = 35;           // 每个雷的边长
        #endregion

        #region 1280*1024分辨率下的参数设置
        //static int M_up = 100;          //半屏下 扫雷的矩形区域为 35*35，第一个雷的左上角坐标为（75，115）
        //static int M_left = 60;         //偏移量，可以在半屏下截出雷区
        //static int step = 35;           // 每个雷的边长
        #endregion

        static int row = 16;            // 列数 16*16
        static int ThValue = 125;       //老板电脑上识别不出来就是阈值选取过大,我的电脑上设为150
        static bool DebugFlag = false;  //是否调试，即保存图片到电脑上
        static double waitTime = 0.3;   //鼠标点击后，需要暂停的时间,在我电脑上，0.3s是极限
        static int M_num_limit = 30;    //当已知雷的个数大于M_num_limit，且要随机选的时候，跳出，不随机选

        static int mtotal = 40;         //总共雷的个数
        static int M_num = 0;           //已找到雷的个数
        static int _num = 0;            //已经确定不是雷的未翻开个数

        static bool BoomFlag = false;       //是否显示已知的雷
        static int[,] mat = new int[16, 16]; //保存雷区的状态，0表示未知，1-8分别表示周围雷的个数，-1表示确认有雷,-2表示已点开，是空白，-3表示未知，但知道不是雷，可点
        static uint prePressTime = 0;       //上次点击时间
        static int pressTime = 0;           //连续点击次数
        static bool MutilFlag = true;       //多次点击的标志，如果为false,就要结束程序
        private Rectangle rt;                //扫雷程序的区域
        
        public Rectangle rect                // 属性
        {
            set { rt = value; }
            get { return rt; }
        }

        #endregion

        #region 加载的动态库

        [System.Runtime.InteropServices.DllImportAttribute("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        static extern bool GetWindowRect(IntPtr hWnd, ref RECT lpRect);
        [DllImport("user32.dll")]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        public struct RECT {
            public int Left; //最左坐标
            public int Top; //最上坐标
            public int Right; //最右坐标
            public int Bottom; //最下坐标 
        }

        public enum MouseEventFlags 
        { 
            Move = 0x0001, 
            LeftDown = 0x0002, 
            LeftUp = 0x0004, 
            RightDown = 0x0008, 
            RightUp = 0x0010, 
            MiddleDown = 0x0020, 
            MiddleUp = 0x0040, 
            Wheel = 0x0800, 
            Absolute = 0x8000 
        } 
        [DllImport("user32.dll")] 
            private static extern int SetCursorPos(int x, int y); 
        [DllImport("User32")] 
            public extern static void mouse_event(int dwFlags, int dx, int dy, int dwData, IntPtr dwExtraInfo);



        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        static extern uint GetTickCount();

        #endregion

        #region 辅助方法
        /// <summary>
        /// 暂停函数
        /// </summary>
        /// <param name="time"></param>
        void pause(double time)
        {
            uint start = GetTickCount();  //获得当前系统时间
            while (GetTickCount() - start < time*1000)
            {
                //do Nothing..
            }
        }

        /// <summary>
        /// 移动鼠标到（x,y）处点击
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        void pressMouse(int x, int y) 
        {
            uint start = GetTickCount();  //获得当前系统时间
            if (start - prePressTime < (waitTime + 0.1) * 1000)
            {
                pressTime++;
            }
            else
            {
                pressTime = 0;
            }

            if (pressTime > mtotal/2)
            {
                Console.WriteLine("点击次数过多，已停止！");
                _num = 0;
                MutilFlag = false;
                return;
            }

            SetCursorPos(x,y);        //移动点（x,y）处
            mouse_event((int)(MouseEventFlags.LeftDown | MouseEventFlags.LeftUp), 0, 0, 0, IntPtr.Zero);  //点击
            pause(waitTime);                     //等待0.3s
            prePressTime = GetTickCount();
        }


        void pressMouseBoom(int x, int y)
        {
            //SetCursorPos(rt.Left + M_left + x * step + step / 2,rt.Top + M_up + y * step + step / 2);        //移动点（x,y）处
            SetCursorPos(rt.Left + M_left + y * step + step / 2, rt.Top + M_up + x * step + step / 2);
            mouse_event((int)(MouseEventFlags.RightDown | MouseEventFlags.RightUp), 0, 0, 0, IntPtr.Zero);  //点击
            pause(0.1);                     //等待0.3s
        }

        /// <summary>
        /// 图像的二值化
        /// </summary>
        /// <param name="img1"></param>
        /// <param name="picName"></param>
        void ToGrey(Bitmap img1,string picName){       
            for (int i = 0; i < img1.Width; i++)       
            {         
                for (int j = 0; j < img1.Height; j++)         
                {           
                    Color pixelColor = img1.GetPixel(i, j);           //计算灰度值           
                    int grey = (int)(0.299 * pixelColor.R + 0.587 * pixelColor.G + 0.114 * pixelColor.B);           
                    Color newColor = Color.FromArgb(grey, grey, grey);           
                    img1.SetPixel(i, j, newColor);         
                }      
            }
            img1.Save(@"D:" + picName + ".png");
        }

        /// <summary>
        /// 像素点的灰度化
        /// </summary>
        /// <param name="pixelColor"></param>
        /// <returns></returns>
        int toGrey(Color pixelColor)
        {
            return (int)(0.299 * pixelColor.R + 0.587 * pixelColor.G + 0.114 * pixelColor.B);
        }

        #endregion

        #region 图像方法，主要为了得到雷区的数字
        /// <summary>
        /// 获得当前扫雷的图像
        /// </summary>
        /// <param name="rt"></param>
        Bitmap getImage()
        {
            Bitmap originBmp = new Bitmap(rt.Width - M_left, rt.Height - M_up);   //可以去掉标题栏等不必要的东西,只留雷区
            using (Graphics gs = Graphics.FromImage(originBmp))
            {
                //复制当前屏幕到画板上，即将截屏图片的内容设置为当前屏幕
                gs.CopyFromScreen(rt.Left + M_left, rt.Top + M_up, 0, 0, rt.Size);
            }

            if(DebugFlag)
                originBmp.Save(@"D:\\2\\" + GetTickCount().ToString() + ".png");

            return originBmp;
        }

        
        /// <summary>
        /// 将前后得到的两张图片相减
        /// 并将得到的图像保存在 D:\\out.png
        /// </summary>
        /// <param name="img1"></param>
        /// <param name="img2"></param>
        /// <param name="ThValue"></param>
        Bitmap ImgSub(Bitmap img1, Bitmap img2) 
        {
            int w = img1.Width;
            int h = img1.Height;
            Bitmap outImg = new Bitmap(w, h);

            for (int i = 0; i < w; i++) 
            {
                for (int j = 0; j < h; j++) 
                {
                    Color p1 = img1.GetPixel(i, j);
                    Color p2 = img2.GetPixel(i, j);
                    int SubValue = Math.Abs(toGrey(p1) - toGrey(p2)); 
                    if (SubValue > ThValue)         //设置阈值
                        outImg.SetPixel(i, j, Color.FromArgb(255, 255, 255)); 
                    else
                        outImg.SetPixel(i, j, Color.FromArgb(0, 0, 0));
                } 
            }

            if(DebugFlag)
                outImg.Save(@"D:\\2\\out\\" + GetTickCount().ToString() + ".png");

            return outImg;
        }

        /// <summary>
        /// 处理得到的二值差分图像
        /// 得到已经显示出来的雷区数字
        /// 并删去 D:\\out.png
        /// </summary>
        bool deal2Img(Bitmap outImg, Bitmap Img2)
        {
            //if (File.Exists(@"D:\\out.png"))
                //Bitmap outImg = new Bitmap("D:\\out.png", true);
            int rowline = 0;
            //Bitmap Img = new Bitmap(outImg.Width, outImg.Height);
            int flag = 0;   //如果前后两张都没有任何不同，那么返回失败
            for (int i = 0; i < row; i++) //行
            {
                rowline = i * step + step / 2;
                for (int j = 0; j < outImg.Width; j++)  //列
                {
                    Color p = outImg.GetPixel(j, rowline);      //图像存储是(h,w)

                    if (p.R > 100)
                    {
                        flag++;
                        int col = (j-step/3) / step;            // 减去1/3 step是为了防止刚好整除，得到的结果大了1
                        int res = judgeAreo(Img2,col, i);

                        if (col < 0 || col >= row)
                        {
                            Console.WriteLine("下标出错！,flag = {0}",flag);
                            return false;
                        }

                        //if (mat[i, col] > 0)
                        //{
                        //    Console.WriteLine("找到已出现的雷！[{0},{1}],flag={2}",i,col,flag);
                        //    return false;
                        //}

                        //调试
                        mat[i,col] = res == 0 ? -2 : res;

                        j += step / 2;  //移除相邻的阴影产生的线条的影响
                    }

                    //outImg.SetPixel(j, rowline, Color.FromArgb(255, 0, 0)); 
                }
            }
                //outImg.Save(@"D:\\out1.png");  
            if (flag == 0)
            {
                Console.WriteLine("没有找到不同点");
                return false;
            }

            //Console.WriteLine("找到{0}个不同点", flag);
            return true;        
        }

        /// <summary>
        /// 判断mat[i,j]的数字是几
        /// </summary>
        /// <param name="sImg"></param>
        /// <param name="i"></param>
        /// <param name="j"></param>
        /// <returns></returns>
        int judgeAreo(Bitmap sImg,int ii, int jj) 
        {
            int[] res = new int[9]; //0-8
            int x = ii * step;
            int y = jj * step;
            //Bitmap sImg = new Bitmap("D:\\2.png", true);
            for (int i = step/2; i < step; i++) 
            {
                for (int j = step/2; j < step; j++) 
                {
                    res[judgeNum(sImg.GetPixel(x + i, y + j))]++;
                }
            }


            #region 将找到的图截到"D:\\2\\sub"文件夹里面
            if (DebugFlag)
            {
                Rectangle cloneRect = new Rectangle(x, y, step, step);
                Bitmap originBmp = sImg.Clone(cloneRect, sImg.PixelFormat);
                originBmp.Save(@"D:\\2\\sub\\" + GetTickCount().ToString() + ".png");
            }
            #endregion

            //判断最大元素的下标
            int max = 0;

            for (int i = 1; i < 9;i++ )
            {
                max = res[i] > max ? res[i] : max;
            }

            if (max == 0)
                return 0;

            for (int i = 4; i >0 ; i--)
            {
                if (res[i] == max)
                    return i;
            }

            return 0;
        }

        /// <summary>
        /// 打印数组
        /// </summary>
        void print() 
        {
            for (int i = 0; i < row; i++) 
            {
                for (int j = 0; j < row; j++) 
                {
                    if (mat[i, j] >= 0)
                        Console.Write("{0}   ", mat[i, j]);
                    else
                        Console.Write("{0}  ", mat[i, j]);
                }
                Console.WriteLine();
            }
        }

        /// <summary>
        /// 将雷区的第i行，第j列的图片截出来并保存
        /// </summary>
        /// <param name="i"></param>
        /// <param name="j"></param>
        void cutNum(int i, int j,String num) 
        {
            Bitmap originBmp = new Bitmap(step,step);   //可以去掉标题栏等不必要的东西,只留雷区
            Size size = new Size(step,step);

            using (Graphics gs = Graphics.FromImage(originBmp))
            {
                //复制当前屏幕到画板上，即将截屏图片的内容设置为当前屏幕
                gs.CopyFromScreen(rt.Left + M_left + i*step, rt.Top + M_up + j*step, 0, 0, size);
            }

            originBmp.Save(@"D:\\" + num + ".png");
            originBmp.Dispose();
            originBmp = null;
        }

        /// <summary>
        /// 从颜色判断点p属于那一个数组
        /// 1 = > R：60-70 G:75-85 B:185-195
        /// 2 = > R：18-45 G:100-110 B:1-10
        /// 3 = > R：>160  G:<15  B:<15
        /// 4 = > R：<5 G:<5 B:120-150
        /// 5 = > R:120-135 G< 5 B < 5
        /// 6 = > R:<10 G:115-135 B:110-140
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        int judgeNum(Color p) 
        {
            if (p.R >= 60 && p.R <= 70 &&
                p.G >= 75 && p.G <= 85 &&
                p.B >= 185 && p.B <= 195)
                return 1;

            if (p.R >= 18 && p.R <= 45 &&
                p.G >= 100 && p.G <= 110 &&
                p.B >= 1 && p.B <= 10)
                return 2;

            if (p.R >= 160 && 
                p.G <= 15  &&
                p.B <= 15)
                return 3;

            if (p.R <= 5 &&
                p.G <=5  &&
                p.B >= 120 && p.B <= 150)
                return 4;

            if (p.R >= 120 && p.R <= 135 &&
                p.G <= 5 &&
                p.B <= 5)
                return 5;

            if (p.R <= 10 &&
                  p.G >= 115 && p.G <=135 &&
                  p.B >= 110 && p.B <= 140)
                return 6;

            return 0;
        }
        #endregion

        void findBoom() 
        {
            for(int i=0;i<row;i++)
                for (int j = 0; j < row; j++) 
                {
                    if(mat[i, j] == -1) //表明此点是炸弹
                        pressMouseBoom(i,j);//右键点击炸弹
                }
        }

        bool run() 
        {
            #region 先判断四周的边界

            #region//先判断（0,0）,(0,row-1),(row-1,0),(row-1,row-1)位置的点

            #region 点（0,0）
            if (mat[0, 0] > 0) 
            {
                int[] p = new int[]{mat[0,1],mat[1,0],mat[1,1]};
                int flag0 = 0,flag1=0;//分别周围存储0和-1的个数
                foreach (int ip in p) 
                {
                    if (ip == 0) flag0++;
                    if (ip == -1) flag1++;
                }

                if (mat[0, 0] >= flag1 + flag0) //如果显示的雷数，大于周围已知的雷数和未知的和，则未知的都是雷
                {
                    if (mat[0, 1] == 0)
                    {
                        mat[0, 1] = -1;
                        M_num++;
                    }

                    if (mat[1, 1] == 0)
                    {
                        mat[1, 1] = -1;
                        M_num++;
                    }

                    if (mat[1, 0] == 0)
                    {
                        mat[1, 0] = -1;
                        M_num++;
                    }
                }
                if (mat[0, 0] == flag1)    //表明周围的雷都已经知道了，未知的都不是雷，可以点
                {
                    if (mat[0, 1] == 0)
                    {
                        mat[0, 1] = -3;    //-3表示未知，但是知道不是雷，可以点的位置
                        _num++;
                    }

                    if (mat[1, 1] == 0)
                    {
                        mat[1, 1] = -3;
                        _num++;
                    }

                    if (mat[1, 0] == 0)
                    {
                        mat[1, 0] = -3;
                        _num++;
                    }
                }

                if (flag0 == 0 && flag1 == 0) //四周都知道了，所以是内部空白点
                {
                    mat[0, 0] = -2;
                    //Console.WriteLine("将[{0},{1}]置为-2", 0, 0);
                }

            }
            #endregion

            #region  点（0,row-1）
            if (mat[0, row - 1] > 0)
            {
                int[] p = new int[] { mat[0, row - 2], mat[1, row - 2], mat[1, row - 1] };
                int flag0 = 0, flag1 = 0;//分别周围存储0和-1的个数
                foreach (int ip in p)
                {
                    if (ip == 0) flag0++;
                    if (ip == -1) flag1++;
                }

                if (mat[0, row - 1] >= flag1 + flag0) //如果显示的雷数，大于周围已知的雷数和未知的和，则未知的都是雷
                {
                    if (mat[0, row - 2] == 0)
                    {
                        mat[0, row - 2] = -1;
                        M_num++;
                    }

                    if (mat[1, row - 2] == 0)
                    {
                        mat[1, row - 2] = -1;
                        M_num++;
                    }

                    if (mat[1, row - 1] == 0)
                    {
                        mat[1, row - 1] = -1;
                        M_num++;
                    }
                }

                if (mat[0, row - 1] == flag1)    //表明周围的雷都已经知道了，未知的都不是雷，可以点
                {
                    if (mat[0, row - 2] == 0)
                    {
                        mat[0, row - 2] = -3;    //-3表示未知，但是知道不是雷，可以点的位置
                        _num++;
                    }

                    if (mat[1, row - 2] == 0)
                    {
                        mat[1, row - 2] = -3;
                        _num++;
                    }

                    if (mat[1, row - 1] == 0)
                    {
                        mat[1, row - 1] = -3;
                        _num++;
                    }
                    
                }

                if (flag0 == 0 && flag1 == 0) //四周都知道了，所以是内部空白点
                {
                    mat[0, row - 1] = -2;
                    //Console.WriteLine("将[{0},{1}]置为-2", 0, row - 1);
                }

            }
            #endregion

            #region 点（row-1,0）
            if (mat[row - 1, 0] > 0)
            {
                int[] p = new int[] { mat[row - 1, 1], mat[row - 2, 0], mat[row - 2, 1] };
                int flag0 = 0, flag1 = 0;//分别周围存储0和-1的个数
                foreach (int ip in p)
                {
                    if (ip == 0) flag0++;
                    if (ip == -1) flag1++;
                }

                if (mat[row - 1, 0] >= flag1 + flag0) //如果显示的雷数，大于周围已知的雷数和未知的和，则未知的都是雷
                {
                    if (mat[row - 1, 1] == 0)
                    {
                        mat[row - 1, 1] = -1;
                        M_num++;
                    }

                    if (mat[row - 2, 0] == 0)
                    {
                        mat[row - 2, 0] = -1;
                        M_num++;
                    }

                    if (mat[row - 2, 1] == 0)
                    {
                        mat[row - 2, 1] = -1;
                        M_num++;
                    }
                }

                if (mat[row - 1, 0] == flag1)    //表明周围的雷都已经知道了，未知的都不是雷，可以点
                {
                    if (mat[row - 1, 1] == 0)
                    {
                        mat[row - 1, 1] = -3;    //-3表示未知，但是知道不是雷，可以点的位置
                        _num++;
                    }

                    if (mat[row - 2, 0] == 0)
                    {
                        mat[row - 2, 0] = -3;
                        _num++;
                    }

                    if (mat[row - 2, 1] == 0)
                    {
                        mat[row - 2, 1] = -3;
                        _num++;
                    }
                }

                if (flag0 == 0 && flag1 == 0) //四周都知道了，所以是内部空白点
                {
                    mat[row - 1, 1] = -2;
                    //Console.WriteLine("将[{0},{1}]置为-2", row - 2, 1);
                }

            }
            #endregion

            #region  点（row-1,row-1）
            if (mat[row - 1, row - 1] > 0)
            {
                int[] p = new int[] { mat[row - 1, row - 2], mat[row - 2, row - 2], mat[row - 2, row - 1] };
                int flag0 = 0, flag1 = 0;//分别周围存储0和-1的个数
                foreach (int ip in p)
                {
                    if (ip == 0) flag0++;
                    if (ip == -1) flag1++;
                }

                if (mat[row - 1, row - 1] >= flag1 + flag0) //如果显示的雷数，大于周围已知的雷数和未知的和，则未知的都是雷
                {
                    if (mat[row - 1, row - 2] == 0)
                    {
                        mat[row - 1, row - 2] = -1;
                        M_num++;
                    }

                    if (mat[row - 2, row - 2] == 0)
                    {
                        mat[row - 2, row - 2] = -1;
                        M_num++;
                    }

                    if (mat[row - 2, row - 1] == 0)
                    {
                        mat[row - 2, row - 1] = -1;
                        M_num++;
                    }
                }

                if (mat[row - 1, row - 1] == flag1)    //表明周围的雷都已经知道了，未知的都不是雷，可以点
                {
                    if (mat[row - 1, row - 2] == 0)
                    {
                        mat[row - 1, row - 2] = -3;    //-3表示未知，但是知道不是雷，可以点的位置
                        _num++;
                    }

                    if (mat[row - 2, row - 2] == 0)
                    {
                        mat[row - 2, row - 2] = -3;
                        _num++;
                    }

                    if (mat[row - 2, row - 1] == 0)
                    {
                        mat[row - 2, row - 1] = -3;
                        _num++;
                    }
                }

                if (flag0 == 0 && flag1 == 0) //四周都知道了，所以是内部空白点
                {
                    mat[row - 1, row - 1] = -2;
                    //Console.WriteLine("将[{0},{1}]置为-2", row - 2, row - 1);
                }

            }
            #endregion

            #endregion

            #region 最上和最下两行的处理
            for (int j = 1; j < row - 1; j++)
            {
                foreach (int i in new int[] { 0, row - 1 }) 
                {
                    int index = i == row - 1 ? row - 2 : 1; //下标

                    if (mat[i, j] > 0)
                    {
                        int flag0 = 0, flag1 = 0;

                        for (int x = -1; x < 2; x++)
                        {
                            if (mat[i, j + x] == 0)
                            {
                                if (x == 0) continue;
                                flag0++;
                            }

                            if (mat[i, j + x] == -1)
                            {
                                if (x == 0) continue;
                                flag1++;
                            }

                            if (mat[index, j + x] == 0) flag0++;
                            if (mat[index, j + x] == -1) flag1++;
                        }

                        if (mat[i, j] >= flag1 + flag0) //如果显示的雷数，大于周围已知的雷数和未知的和，则未知的都是雷
                        {
                            for (int x = -1; x < 2; x++)
                            {
                                if (mat[i, j + x] == 0)
                                {
                                    mat[i, j + x] = -1;
                                    M_num++;
                                }
                                if (mat[index, j + x] == 0)
                                {
                                    mat[index, j + x] = -1;
                                    M_num++;
                                }
                            }
                        }

                        if (mat[i, j] == flag1)    //表明周围的雷都已经知道了，未知的都不是雷，可以点
                        {
                            for (int x = -1; x < 2; x++)
                            {
                                if (mat[i, j + x] == 0)
                                {
                                    mat[i, j + x] = -3;//-3表示未知，但是知道不是雷，可以点的位置
                                    _num++;
                                    //Console.WriteLine("在[{0},{1}]发现{2},{3}处是非雷", i, j, i, j + x);
                                }
                                if (mat[index, j + x] == 0)
                                {
                                    mat[index, j + x] = -3;
                                    _num++;
                                    //Console.WriteLine("在[{0},{1}]发现{2},{3}处是非雷", i, j, index, j + x);
                                }
                            }
                        }

                        if (flag0 == 0 && flag1 == 0) //四周都知道了，所以是内部空白点
                        {
                            mat[i, j] = -2;
                            //Console.WriteLine("将[{0},{1}]置为-2", i, j);
                        }


                    }
                }
            }
            #endregion

            #region 最左和最右两列的处理

            for (int i = 1; i < row - 1; i++)
            {
                foreach (int j in new int[] { 0, row - 1 })
                {
                    int index = j == row - 1 ? row - 2 : 1; //下标

                    if (mat[i, j] > 0)
                    {
                        int flag0 = 0, flag1 = 0;

                        for (int x = -1; x < 2; x++)
                        {
                            if (mat[i + x, j] == 0)
                            {
                                if (x == 0) continue;
                                flag0++;
                            }

                            if (mat[i + x, j] == -1)
                            {
                                if (x == 0) continue;
                                flag1++;
                            }

                            if (mat[i + x, index] == 0) flag0++;
                            if (mat[i + x, index] == -1) flag1++;
                        }

                        if (mat[i, j] >= flag1 + flag0) //如果显示的雷数，大于周围已知的雷数和未知的和，则未知的都是雷
                        {
                            for (int x = -1; x < 2; x++)
                            {
                                if (mat[i + x, j] == 0)
                                {
                                    mat[i + x, j] = -1;
                                    M_num++;
                                }
                                if (mat[i + x, index] == 0)
                                {
                                    mat[i + x, index] = -1;
                                    M_num++;
                                }
                            }
                        }

                        if (mat[i, j] == flag1)    //表明周围的雷都已经知道了，未知的都不是雷，可以点
                        {
                            for (int x = -1; x < 2; x++)
                            {
                                if (mat[i + x, j] == 0)
                                {
                                    mat[i + x, j] = -3;
                                    _num++;
                                    //Console.WriteLine("在[{0},{1}]发现{2},{3}处是非雷", i, j, i + x, j);
                                }
                                if (mat[i + x, index] == 0)
                                {
                                    mat[i + x, index] = -3;
                                    _num++;
                                    //Console.WriteLine("在[{0},{1}]发现{2},{3}处是非雷", i, j, i + x, index);
                                }
                            }
                        }

                        if (flag0 == 0 && flag1 == 0) //四周都知道了，所以是内部空白点
                        {
                            mat[i, j] = -2;
                            //Console.WriteLine("将[{0},{1}]置为-2", i, j);
                        }
                    }
                }
            }
            #endregion

            #endregion

            #region 再考虑除了边界外的内部
            for (int i = 1; i < row - 1; i++)//不考虑边界
            {
                for (int j = 1; j < row - 1; j++)
                {
                    if (mat[i, j] > 0)
                    {
                        int flag0 = 0,flag1 = 0;
                        for (int x = -1; x < 2; x++)
                            for (int y = -1; y < 2; y++)
                            {
                                if (x == 0 && y == 0)
                                    continue;
                                if (mat[i + x, j + y] == 0) flag0++;
                                if (mat[i + x, j + y] == -1) flag1++;
                            }

                        #region 找到炸弹的点
                        if (mat[i, j] >= flag0 + flag1)   //表明附近的点肯定是炸弹
                        {
                            for (int x = -1; x < 2; x++)
                                for (int y = -1; y < 2; y++)
                                {
                                    if (x == 0 && y == 0)
                                        continue;
                                    if (mat[i + x, j + y] == 0)
                                    {
                                        mat[i + x, j + y] = -1; //表明此点是炸弹
                                        M_num++;                //找到的炸弹数量加1
                                        //Console.WriteLine("由[{2},{3}]发现位置[{0},{1}]是炸弹", i + x, j + y, i, j);
                                        //pressMouseBoom(rt.Left + M_left + (j + y) * step + step / 2, rt.Top + M_up + (i + x) * step + step / 2);//知道是炸弹，并不去点
                                    }
                                }
                        }
                        #endregion

                        if (mat[i, j] == flag1)   //表明周围的雷都已经知道了，未知的都不是雷，可以点
                        {
                            for (int x = -1; x < 2; x++)
                                for (int y = -1; y < 2; y++)
                                {
                                    if (x == 0 && y == 0)
                                        continue;
                                    if (mat[i + x, j + y] == 0)
                                    {
                                        mat[i + x, j + y] = -3; //表明此点肯定不是炸弹
                                        _num++;
                                        //Console.WriteLine("在[{0},{1}]发现{2},{3}处是非雷", i, j, i + x, j + y);
                                    }
                                }
                        }

                        if (flag0 == 0 && flag1 == 0) //表明附近没有不知道的雷
                        {
                            mat[i, j] = -2;
                        }

                    }
                }
            }
            #endregion

            #region  点击已经找到的雷点
            if (_num > 0)
            {
                //Console.WriteLine("已经找到了{0}个非雷区", _num);
                for (int i = 0; i < row; i++)
                    for (int j = 0; j < row; j++)
                        if (mat[i, j] == -3)
                        {
                            pressMouse(rt.Left + M_left + j * step + step / 2, rt.Top + M_up + i * step + step / 2);
                        }
                _num = 0;
                return true;
            }

            #endregion

            #region 没有找到可选炸弹的点,随机点一下，从值为的1的周围开始找，一直到值为8
            int xx=-1, yy=-1;
            uint start = GetTickCount();  //获得当前系统时间
            if (M_num == mtotal) //如果找到雷的数量等于所有的，那么随便点
            {
                for (int i = 0; i < row; i++)
                    for (int j = 0; j < row; j++)
                        if (mat[i, j] == 0)
                        {
                            pressMouse(rt.Left + M_left + j * step + step / 2, rt.Top + M_up + i * step + step / 2);
                            return true;
                        }
            }
            else
            {
                if (M_num > M_num_limit)
                {
                    BoomFlag = true;
                    return false;
                }
                do
                {
                    for (int k = 1; k < 9; k++)
                    {
                        for (int i = 0; i < row; i++)
                        {
                            for (int j = 0; j < row; j++)
                            {
                                if (mat[i, j] == k)
                                {
                                    #region  (0,0)
                                    if (i == 0 && j == 0)
                                    {
                                        if (mat[0, 1] == 0)
                                        {
                                            xx = 0;
                                            yy = 1;
                                            goto label1;
                                        }
                                        if (mat[1, 1] == 0)
                                        {
                                            xx = 1;
                                            yy = 1;
                                            goto label1;
                                        }

                                        if (mat[1, 0] == 0)
                                        {
                                            xx = 1;
                                            yy = 0;
                                            goto label1;
                                        }

                                        continue;
                                    }
                                    #endregion

                                    #region  (0,w-1)
                                    if (i == 0 && j == row - 1)
                                    {
                                        if (mat[0, row - 2] == 0)
                                        {
                                            xx = 0;
                                            yy = row - 2;
                                            goto label1;
                                        }
                                        if (mat[1, row - 1] == 0)
                                        {
                                            xx = 1;
                                            yy = row - 1;
                                            goto label1;
                                        }

                                        if (mat[1, row - 2] == 0)
                                        {
                                            xx = 1;
                                            yy = row - 2;
                                            goto label1;
                                        }

                                        continue;
                                    }
                                    #endregion

                                    #region  (w-1,0)
                                    if (i == row - 1 && j == 0)
                                    {
                                        if (mat[row - 2, 0] == 0)
                                        {
                                            xx = row - 2;
                                            yy = 0;
                                            goto label1;
                                        }
                                        if (mat[row - 1, 1] == 0)
                                        {
                                            yy = 1;
                                            xx = row - 1;
                                            goto label1;
                                        }

                                        if (mat[row - 2, 1] == 0)
                                        {
                                            yy = 1;
                                            xx = row - 2;
                                            goto label1;
                                        }

                                        continue;
                                    }
                                    #endregion

                                    #region  (w-1,w-1)
                                    if (i == row - 1 && j == row - 1)
                                    {
                                        if (mat[row - 2, row - 2] == 0)
                                        {
                                            xx = row - 2;
                                            yy = row - 2;
                                            goto label1;
                                        }
                                        if (mat[row - 2, row - 1] == 0)
                                        {
                                            xx = row - 2;
                                            yy = row - 1;
                                            goto label1;
                                        }

                                        if (mat[row - 1, row - 2] == 0)
                                        {
                                            xx = row - 1;
                                            yy = row - 2;
                                            goto label1;
                                        }

                                        continue;
                                    }
                                    #endregion

                                    #region 上下两行
                                    if (i == 0 || i == row - 1)
                                    {
                                        int Ni = i == 0 ? 1 : row - 2;
                                        for (int ip = -1; ip < 2; ip++)
                                        {
                                            if (mat[i, j + ip] == 0)
                                            {
                                                xx = i;
                                                yy = j + ip;
                                                goto label1;
                                            }
                                            if (mat[Ni, j + ip] == 0)
                                            {
                                                xx = Ni;
                                                yy = j + ip;
                                                goto label1;
                                            }
                                        }

                                        continue;
                                    }
                                    #endregion

                                    #region 左右两列
                                    if (j == 0 || j == row - 1)
                                    {
                                        int Ni = j == 0 ? 1 : row - 2;
                                        for (int ip = -1; ip < 2; ip++)
                                        {
                                            if (mat[i + ip, j] == 0)
                                            {
                                                xx = i + ip;
                                                yy = j;
                                                goto label1;
                                            }
                                            if (mat[i + ip, Ni] == 0)
                                            {
                                                xx = i + ip;
                                                yy = Ni;
                                                goto label1;
                                            }
                                        }

                                        continue;
                                    }
                                    #endregion

                                    #region 四周
                                    for (int id = -1; id < 2; id++)
                                        for (int jd = -1; jd < 2; jd++)
                                        {
                                            if (id == 0 && jd == 0)
                                                continue;
                                            if (mat[i + id, j + jd] == 0)
                                            {
                                                xx = i + id;
                                                yy = j + jd;
                                                goto label1;
                                            }
                                        }
                                    #endregion

                                }
                            }
                        }
                    }
                } while ((GetTickCount() - start < 2000));
            }

            label1:
            if (xx == -1 && yy == -1) 
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("时间过长，本程也无能为力了  ╮(╯▽╰)╭  ");
                Console.ForegroundColor = ConsoleColor.White;
                //findBoom(); //按出炸弹所在的位置
                return false;
            }

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Warning: 随机选择[{0}, {1}]的位置进行点击", xx, yy);
            Console.ForegroundColor = ConsoleColor.Gray;

            //app.print();
            pressMouse(rt.Left + M_left + yy * step + step / 2, rt.Top + M_up + xx * step + step / 2);
            #endregion

            return true&MutilFlag;
        }
        
        static void Main(string[] args)
        {
            Bitmap img1, img2,ImgSub;
            Program app = new Program();
            //Rectangle rt = new Rectangle(0,0,712,760);
            /*
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\t\t*-------------------说明------------------*");
            Console.WriteLine("\t\t|                                         |");
            Console.WriteLine("\t\t|    确保在win7系统下,分辨率为1366*768    |");
            Console.WriteLine("\t\t|                                         |");
            Console.WriteLine("\t\t|    扫雷难度为中级 且需要左半屏最大化    |");
            Console.WriteLine("\t\t|                                         |");
            Console.WriteLine("\t\t|   半屏最大化即拖动程序窗口触屏屏幕左边  |");
            Console.WriteLine("\t\t|                                         |");
            Console.WriteLine("\t\t|   运行本程序后按下回车三秒后开始游戏    |");
            Console.WriteLine("\t\t|                                         |");
            Console.WriteLine("\t\t|  运行本程序后 点下扫雷 确保其为当前程序 |");
            Console.WriteLine("\t\t|                                         |");
            Console.WriteLine("\t\t*-----------------------------------------*");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.ReadLine();
            Console.Clear();
            Console.WriteLine("3秒后程序开始..请确保扫雷其为当前程序");
            app.pause(3.0);                        //等待三秒，手动激活扫雷程序，使其成为当然活动程序
            */

            IntPtr winmineHandle = FindWindow(null, "扫雷");//找到扫雷游戏窗口
            if (winmineHandle == IntPtr.Zero)
            {
                Console.WriteLine("没有发现扫雷程序正在运行");
                Console.ReadLine();
                return;

            }
            SetForegroundWindow(winmineHandle);
            app.pause(1.0);
            IntPtr awin = GetForegroundWindow(); //获取当前窗口句柄
            RECT rect = new RECT();
            GetWindowRect(awin, ref rect);      //获得扫雷程序所在的矩形区域
            Rectangle rt = new Rectangle(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
            app.rect = rt;

            img1 = app.getImage();
            app.pressMouse(rt.Left + rt.Width / 2, rt.Top + rt.Height / 2);
            Console.WriteLine("中心点的位置为：{0},{1}", rt.Left + rt.Width / 2, rt.Top + rt.Height / 2);
            bool runFalg = true;
            int numF = 9;
            while (runFalg)
            {
                if (M_num != M_num_limit)
                {
                    img2 = app.getImage();
                    ImgSub = app.ImgSub(img1, img2);
                    runFalg = app.deal2Img(ImgSub, img2);
                    img1 = img2;
                }
                if (M_num > numF)
                {
                    Console.WriteLine("----------------------------------------------");
                    Console.WriteLine("已经找到了{0}个雷", M_num);
                    numF += 10;
                }

                runFalg = runFalg & app.run();
                //app.print();
            }

            if (BoomFlag)
            {
                Console.WriteLine("--------------**************------------------");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("本程只能做这么多了，接下来靠你了  ╮(╯▽╰)╭");
                app.findBoom(); //按出炸弹所在的位置
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Game Over!");
            Console.Write("Write by ");
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.Write("epleone");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  Thx..");
            Console.ReadLine();


        }
    }
}
